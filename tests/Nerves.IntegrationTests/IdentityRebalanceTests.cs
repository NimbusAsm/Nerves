using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nerves.Identity.Domain;
using Nerves.Identity.Persistence;
using Nerves.Identity.Sharding;
using Nerves.Sharding;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Nerves.IntegrationTests;

/// <summary>Verifies that an identity shard expansion moves account rows.</summary>
public sealed class IdentityRebalanceTests : IAsyncLifetime
{
    private static readonly string[] ShardDatabases = ["shard_a", "shard_b", "shard_c", "shard_d"];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6-alpine").Build();

    private readonly Dictionary<string, string> _connections = new(StringComparer.Ordinal);

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        var admin = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = "postgres",
        };

        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var template = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString());
        foreach (var database in ShardDatabases)
        {
            await using var create = new NpgsqlCommand($"create database {database}", connection);
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

            template.Database = database;
            _connections[database] = template.ConnectionString;
        }
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Expanding_shards_moves_accounts_and_credentials()
    {
        var config = BuildPlaneConfiguration();
        var plane = new IdentityDataPlane(
            new ShardConnectionResolver(config),
            config,
            Options.Create(new ShardingOptions { Enabled = true, ShardCount = 4 }),
            new ShardResolver(Options.Create(new ShardingOptions { Enabled = true, ShardCount = 4 })));

        await MigrateAsync(plane);

        var (accountA, credentialA) = await SeedAsync(_connections["shard_a"], oldShard: 0, newShard: 2);
        var (accountB, credentialB) = await SeedAsync(_connections["shard_b"], oldShard: 1, newShard: 3);
        var accounts = new[] { accountA, accountB };

        var rebalanceConfig = BuildRebalanceConfiguration();
        var rebalancer = new IdentityShardRebalancer(rebalanceConfig, plane, NullLogger<IdentityShardRebalancer>.Instance);

        var dryRun = await rebalancer.RunAsync(true, TestContext.Current.CancellationToken);
        dryRun.Scanned.ShouldBe(2);
        dryRun.Moved.ShouldBe(2);
        (await ExistsAsync(_connections["shard_a"], accounts[0])).ShouldBeTrue();

        var applied = await rebalancer.RunAsync(false, TestContext.Current.CancellationToken);
        applied.Moved.ShouldBe(2);

        foreach (var (accountId, credentialId) in new[] { (accounts[0], credentialA), (accounts[1], credentialB) })
        {
            var target = ShardDatabases[plane.ShardFor(accountId)];
            (await ExistsAsync(_connections[target], accountId)).ShouldBeTrue();
            (await CredentialExistsAsync(_connections[target], credentialId)).ShouldBeTrue();
        }

        (await ExistsAsync(_connections["shard_a"], accounts[0])).ShouldBeFalse();
        (await ExistsAsync(_connections["shard_b"], accounts[1])).ShouldBeFalse();

        var rerun = await rebalancer.RunAsync(false, TestContext.Current.CancellationToken);
        rerun.Moved.ShouldBe(0);
    }

    private IConfiguration BuildPlaneConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
            ["Sharding:Enabled"] = "true",
            ["Sharding:ShardCount"] = "4",
            ["Sharding:ConnectionStrings:0"] = _connections["shard_a"],
            ["Sharding:ConnectionStrings:1"] = _connections["shard_b"],
            ["Sharding:ConnectionStrings:2"] = _connections["shard_c"],
            ["Sharding:ConnectionStrings:3"] = _connections["shard_d"],
        }).Build();

    private IConfiguration BuildRebalanceConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sharding:PreviousShardCount"] = "2",
            ["Sharding:PreviousConnectionStrings:0"] = _connections["shard_a"],
            ["Sharding:PreviousConnectionStrings:1"] = _connections["shard_b"],
        }).Build();

    private static async Task MigrateAsync(IdentityDataPlane plane)
    {
        await using (var global = plane.CreateGlobal())
        {
            await global.Database.MigrateAsync();
        }

        foreach (var options in plane.Shards())
        {
            await using var shard = new IdentityDbContext(options);
            await shard.Database.MigrateAsync();
        }
    }

    private static async Task<(Guid AccountId, Guid CredentialId)> SeedAsync(
        string connection,
        int oldShard,
        int newShard)
    {
        var oldMap = new ShardMap(2);
        var newMap = new ShardMap(4);
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options;
        var now = DateTimeOffset.UtcNow;

        NetworkAccount? chosen = null;
        for (var index = 0; index < 5000; index++)
        {
            var candidate = NetworkAccount.Create(
                Guid.NewGuid(), "nerves", "rebalance-" + Guid.NewGuid().ToString("N")[..12], null, null, null,
                AccountType.Personal, now).Value;

            if (oldMap.Resolve(candidate.Id) == oldShard && newMap.Resolve(candidate.Id) == newShard)
            {
                chosen = candidate;
                break;
            }
        }

        chosen.ShouldNotBeNull();
        var credential = AccountCredential.Create(
            chosen.Id, CredentialType.Password, "hash", "pbkdf2", now).Value;

        await using var db = new IdentityDbContext(options);
        db.NetworkAccounts.Add(chosen);
        db.AccountCredentials.Add(credential);
        await db.SaveChangesAsync();
        return (chosen.Id, credential.Id);
    }

    private static async Task<bool> ExistsAsync(string connection, Guid accountId)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand("select count(*) from identity.\"network_account\" where \"Id\" = @id", db);
        command.Parameters.AddWithValue("id", accountId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<bool> CredentialExistsAsync(string connection, Guid credentialId)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand("select count(*) from identity.\"account_credential\" where \"Id\" = @id", db);
        command.Parameters.AddWithValue("id", credentialId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;
    }
}
