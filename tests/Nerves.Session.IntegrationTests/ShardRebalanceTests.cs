using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nerves.Session.Persistence;
using Nerves.Session.Sharding;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Nerves.Session.IntegrationTests;

/// <summary>Verifies that a shard expansion moves rows to their new database.</summary>
public sealed class ShardRebalanceTests : IAsyncLifetime
{
    private static readonly string[] Databases = ["shard_a", "shard_b", "shard_c", "shard_d"];

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
        foreach (var database in Databases)
        {
            await using var create = new NpgsqlCommand($"create database {database}", connection);
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

            template.Database = database;
            _connections[database] = template.ConnectionString;
        }
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Expanding_shards_moves_existing_sessions()
    {
        var store = NewStore();
        await MigrateAsync(store);

        var oldAccounts = PickAccountsForTwoShards();
        var sessionA = await SeedAsync(_connections["shard_a"], oldAccounts[0]);
        var sessionB = await SeedAsync(_connections["shard_b"], oldAccounts[1]);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sharding:PreviousShardCount"] = "2",
            ["Sharding:PreviousConnectionStrings:0"] = _connections["shard_a"],
            ["Sharding:PreviousConnectionStrings:1"] = _connections["shard_b"],
        }).Build();

        var rebalancer = new SessionShardRebalancer(configuration, store, NullLogger<SessionShardRebalancer>.Instance);

        var dryRun = await rebalancer.RunAsync(true, TestContext.Current.CancellationToken);
        dryRun.Scanned.ShouldBe(2);
        dryRun.Moved.ShouldBe(2);
        (await CountAsync(_connections["shard_a"])).ShouldBe(1);

        var applied = await rebalancer.RunAsync(false, TestContext.Current.CancellationToken);
        applied.Moved.ShouldBe(2);

        foreach (var (sessionId, accountId) in new[] { (sessionA, oldAccounts[0]), (sessionB, oldAccounts[1]) })
        {
            var targetDatabase = Databases[store.ShardFor(accountId)];
            var inTarget = await ExistsAsync(_connections[targetDatabase], sessionId);
            inTarget.ShouldBeTrue();
        }

        var expectedA = Databases[store.ShardFor(oldAccounts[0])];
        var expectedB = Databases[store.ShardFor(oldAccounts[1])];
        foreach (var database in Databases)
        {
            var expected = (expectedA == database ? 1 : 0) + (expectedB == database ? 1 : 0);
            (await CountAsync(_connections[database])).ShouldBe(expected);
        }

        var rerun = await rebalancer.RunAsync(false, TestContext.Current.CancellationToken);
        rerun.Moved.ShouldBe(0);
    }

    private SessionStore NewStore()
    {
        var previous = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = _connections["shard_a"],
            ["Sharding:Enabled"] = "true",
            ["Sharding:ShardCount"] = "4",
            ["Sharding:ConnectionStrings:0"] = _connections["shard_a"],
            ["Sharding:ConnectionStrings:1"] = _connections["shard_b"],
            ["Sharding:ConnectionStrings:2"] = _connections["shard_c"],
            ["Sharding:ConnectionStrings:3"] = _connections["shard_d"],
        }).Build();

        var resolver = new Nerves.Sharding.ShardConnectionResolver(previous);
        var options = Microsoft.Extensions.Options.Options.Create(new SessionStoreOptions { Enabled = true });
        return new SessionStore(resolver, options, NullLogger<SessionStore>.Instance);
    }

    private static async Task MigrateAsync(SessionStore store)
    {
        foreach (var options in store.AllOptions())
        {
            await using var db = new SessionDbContext(options);
            await db.Database.MigrateAsync();
        }
    }

    private static List<Guid> PickAccountsForTwoShards()
    {
        var oldMap = new Nerves.Sharding.ShardMap(2);
        var newMap = new Nerves.Sharding.ShardMap(4);
        Guid? first = null;
        Guid? second = null;

        // Both accounts must move databases: old shard 0/1 and new shard 2/3.
        for (var index = 0; index < 5000 && (first is null || second is null); index++)
        {
            var candidate = Guid.NewGuid();
            var oldShard = oldMap.Resolve(candidate);
            var newShard = newMap.Resolve(candidate);
            if (oldShard == 0 && newShard == 2 && first is null) first = candidate;
            if (oldShard == 1 && newShard == 3 && second is null) second = candidate;
        }

        return [first!.Value, second!.Value];
    }

    private static async Task<Guid> SeedAsync(string connection, Guid accountId)
    {
        var options = new DbContextOptionsBuilder<SessionDbContext>().UseNpgsql(connection).Options;
        await using var db = new SessionDbContext(options);
        var session = Nerves.Session.Domain.Session.Create(
            accountId, null, "nerves", "device", "iphash", "uahash", ["pwd"],
            DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30), TimeSpan.FromHours(12));
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private static async Task<int> CountAsync(string connection)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand("select count(*) from session.\"session\"", db);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<bool> ExistsAsync(string connection, Guid sessionId)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand("select count(*) from session.\"session\" where \"Id\" = @id", db);
        command.Parameters.AddWithValue("id", sessionId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;
    }
}
