using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nerves.Identity.Persistence;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Nerves.IntegrationTests;

/// <summary>
/// Boots the identity service with sharding enabled against two databases that
/// live in the same container, and proves that accounts are distributed,
/// resolvable by id and by identifier, listable across shards, and that
/// disabling an account leaves the routing index intact.
/// </summary>
public sealed class IdentityShardingTests : IAsyncLifetime
{
    private const string SecondDatabase = "nerves_identity_shard1";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6-alpine").Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _secondConnectionString = string.Empty;
    private Guid _entityId;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        _secondConnectionString = await CreateSecondDatabaseAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
            builder.UseSetting("Sharding:Enabled", "true");
            builder.UseSetting("Sharding:ShardCount", "2");
            builder.UseSetting("Sharding:ConnectionStrings:0", _postgres.GetConnectionString());
            builder.UseSetting("Sharding:ConnectionStrings:1", _secondConnectionString);
            builder.UseSetting("Database:MigrateOnStart", "false");
            builder.UseSetting("Kestrel:Endpoints", string.Empty);
            builder.UseSetting("DevSeed:Enabled", "false");
            builder.UseSetting(
                "DataProtection:KeysDirectory",
                Path.Combine(Path.GetTempPath(), "nerves-it-keys-" + Guid.NewGuid().ToString("N")));
            builder.UseSetting("Security:IdentifierHashKey", Convert.ToBase64String(new byte[32]));
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    })
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.SchemeName,
                        _ => { });
            });
        });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add(TestAuthenticationHandler.PermissionHeader, "nerves:admin");

        var migrator = _factory.Services.GetRequiredService<IdentityMigrator>();
        await migrator.MigrateAsync(TestContext.Current.CancellationToken);

        _entityId = await CreateEntityAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Accounts_are_distributed_across_the_two_databases()
    {
        var first = await CreateAccountForShardAsync(0);
        var second = await CreateAccountForShardAsync(1);

        (await CountRowsAsync(_postgres.GetConnectionString(), first.Id)).ShouldBe(1);
        (await CountRowsAsync(_secondConnectionString, second.Id)).ShouldBe(1);
        (await CountRowsAsync(_postgres.GetConnectionString(), second.Id)).ShouldBe(0);
        (await CountRowsAsync(_secondConnectionString, first.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Reading_by_id_hits_the_owning_shard()
    {
        var created = await CreateAccountAsync();
        var store = _factory.Services.GetRequiredService<IdentityStore>();
        store.ShardFor(created.Id).ShouldBe(created.Shard);

        var response = await _client.GetAsync($"/api/v1/accounts/{created.Id}", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken);
        payload.ShouldNotBeNull();
        payload!.Id.ShouldBe(created.Id.ToString());
    }

    [Fact]
    public async Task Listing_fans_out_and_returns_accounts_from_both_shards()
    {
        var first = await CreateAccountAsync();
        var second = await CreateAccountForShardAsync(1 - first.Shard);

        var response = await _client.GetAsync("/api/v1/accounts?limit=50", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<PageDto>(TestContext.Current.CancellationToken);
        page.ShouldNotBeNull();
        var ids = page!.Items.Select(x => x.Id).ToList();
        ids.ShouldContain(first.Id.ToString());
        ids.ShouldContain(second.Id.ToString());
    }

    [Fact]
    public async Task Stats_count_accounts_on_every_shard()
    {
        await CreateAccountAsync();
        await CreateAccountAsync();

        var stats = await _client.GetFromJsonAsync<StatsDto>("/api/v1/stats", TestContext.Current.CancellationToken);
        stats.ShouldNotBeNull();
        stats!.Accounts.ShouldBe(2);
        stats.LegalEntities.ShouldBe(1);
    }

    [Fact]
    public async Task An_identifier_resolves_to_the_shard_that_owns_the_account()
    {
        var created = await CreateAccountAsync();
        var store = _factory.Services.GetRequiredService<IdentityStore>();

        await using var global = store.Global();
        var entry = await global.RoutingIndex.AsNoTracking()
            .FirstAsync(x => x.AccountId == created.Id, TestContext.Current.CancellationToken);

        entry.Shard.ShouldBe(created.Shard);

        // Both identifier lookups used by login and recovery fan out and find
        // the account on the shard that owns it.
        var byUsername = await store.FindByUsernameAsync("nerves", created.Username, TestContext.Current.CancellationToken);
        var byEmail = await store.FindByEmailAsync(created.Username + "@acme.test", TestContext.Current.CancellationToken);

        byUsername.ShouldNotBeNull();
        byUsername!.Id.ShouldBe(created.Id);
        byEmail.ShouldNotBeNull();
        byEmail!.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task Disabling_an_account_keeps_the_routing_index_queryable()
    {
        var created = await CreateAccountAsync();

        var disable = await _client.PostAsync(
            $"/api/v1/accounts/{created.Id}/disable", content: null, TestContext.Current.CancellationToken);
        disable.StatusCode.ShouldBe(HttpStatusCode.OK);

        var store = _factory.Services.GetRequiredService<IdentityStore>();
        await using var global = store.Global();
        var stillIndexed = await global.RoutingIndex.AsNoTracking()
            .AnyAsync(x => x.AccountId == created.Id, TestContext.Current.CancellationToken);

        // The identifier is deliberately not released: re-registering a disabled
        // account's links would let a new tenant take over the identity.
        stillIndexed.ShouldBeTrue();

        var read = await _client.GetAsync($"/api/v1/accounts/{created.Id}", TestContext.Current.CancellationToken);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await read.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken);
        payload!.Status.ShouldBe("Disabled");
    }

    [Fact]
    public async Task An_account_row_exists_only_on_its_own_shard_and_entities_stay_global()
    {
        var created = await CreateAccountForShardAsync(0);

        // Exactly one row for this id exists, and it is in the global database
        // because shard 0 is ConnectionStrings:Default.
        (await CountRowsAsync(_postgres.GetConnectionString(), created.Id)).ShouldBe(1);
        (await CountRowsAsync(_secondConnectionString, created.Id)).ShouldBe(0);

        // The legal entity is global: it must NOT be duplicated into the shard.
        await using var shardConnection = new NpgsqlConnection(_secondConnectionString);
        await shardConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var entities = new NpgsqlCommand(
            "select count(*) from identity.legal_entity where \"Id\" = @id", shardConnection);
        entities.Parameters.AddWithValue("id", _entityId);
        Convert.ToInt32(await entities.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture)
            .ShouldBe(0);
    }

    private static string NewUsername() => "shard-" + Guid.NewGuid().ToString("N")[..12];

    private async Task<CreatedAccount> CreateAccountAsync() =>
        await CreateAccountCoreAsync(NewUsername());

    private async Task<CreatedAccount> CreateAccountForShardAsync(int shard)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var created = await CreateAccountCoreAsync(NewUsername());
            if (created.Shard == shard)
            {
                return created;
            }
        }

        throw new InvalidOperationException($"no account mapped to shard {shard} after 40 attempts");
    }

    private async Task<CreatedAccount> CreateAccountCoreAsync(string username)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/accounts", new
        {
            legalEntityId = _entityId,
            realm = "nerves",
            username,
            email = username + "@acme.test",
            displayName = username,
            password = "Str0ng!Pass",
        }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        var id = Guid.Parse(created!.Id);
        var store = _factory.Services.GetRequiredService<IdentityStore>();
        return new CreatedAccount(id, store.ShardFor(id), username);
    }

    private async Task<Guid> CreateEntityAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/legal-entities", new
        {
            type = "LegalPerson",
            displayName = "Shard Test Ltd",
            documentType = "UnifiedSocialCreditCode",
            documentHash = Guid.NewGuid().ToString("N") + new string('0', 32),
            country = "CN",
        }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entity = await response.Content.ReadFromJsonAsync<EntityDto>(TestContext.Current.CancellationToken);
        return Guid.Parse(entity!.Id);
    }

    private static async Task<int> CountRowsAsync(string connectionString, Guid accountId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "select count(*) from identity.network_account where \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", accountId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
    }

    private async Task<string> CreateSecondDatabaseAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = "postgres",
        };

        await using (var connection = new NpgsqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand($"create database {SecondDatabase}", connection);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        builder.Database = SecondDatabase;
        return builder.ConnectionString;
    }

    private sealed record CreatedAccount(Guid Id, int Shard, string Username);

    private sealed record AccountDto(string Id, string Status);

    private sealed record PageDto(IReadOnlyList<AccountDto> Items, string? NextCursor);

    private sealed record EntityDto(string Id);

    private sealed record StatsDto(int Accounts, int LegalEntities);
}
