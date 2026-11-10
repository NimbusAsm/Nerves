using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nerves.Session.Persistence;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Nerves.Session.IntegrationTests;

/// <summary>
/// Boots the session service against two databases and proves that accounts
/// route to their shard, lookups fan out, and revocation works across shards.
/// </summary>
public sealed class SessionShardingTests : IAsyncLifetime
{
    private const string SecondDatabase = "nerves_shard1";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6-alpine").Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _secondConnectionString = string.Empty;

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
            builder.UseSetting("Session:ExpirySweepSeconds", "3600");
            builder.UseSetting("Kestrel:Endpoints", string.Empty);
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

        var migrator = _factory.Services.GetRequiredService<SessionMigrator>();
        await migrator.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Accounts_land_in_different_databases()
    {
        var (first, second) = PickAccountsForDifferentShards();

        await CreateSessionAsync(first);
        await CreateSessionAsync(second);

        var shard0 = await CountSessionsAsync(_postgres.GetConnectionString(), first);
        var shard1 = await CountSessionsAsync(_secondConnectionString, second);

        shard0.ShouldBe(1);
        shard1.ShouldBe(1);
        (await CountSessionsAsync(_postgres.GetConnectionString(), second)).ShouldBe(0);
        (await CountSessionsAsync(_secondConnectionString, first)).ShouldBe(0);
    }

    [Fact]
    public async Task Listing_returns_the_accounts_sessions_from_its_shard()
    {
        var accountId = PickAccountForShard(1);
        await CreateSessionAsync(accountId);

        var response = await _client.GetAsync($"/api/v1/accounts/{accountId}/sessions", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<List<SessionListItem>>(TestContext.Current.CancellationToken);
        payload.ShouldNotBeNull();
        payload.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Revoking_by_session_id_finds_the_owning_shard()
    {
        var accountId = PickAccountForShard(1);
        var sessionId = await CreateSessionAsync(accountId);

        var response = await _client.DeleteAsync($"/api/v1/sessions/{sessionId}", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var revoked = await CountRevokedAsync(_secondConnectionString, sessionId);
        revoked.ShouldBe(1);
    }

    private (Guid First, Guid Second) PickAccountsForDifferentShards()
    {
        var store = _factory.Services.GetRequiredService<SessionStore>();
        Guid? first = null;
        for (var index = 0; index < 500; index++)
        {
            var candidate = Guid.NewGuid();
            if (store.ShardFor(candidate) == 0)
            {
                first = candidate;
                break;
            }
        }

        first.ShouldNotBeNull();
        return (first.Value, PickAccountForShard(1));
    }

    private Guid PickAccountForShard(int shard)
    {
        var store = _factory.Services.GetRequiredService<SessionStore>();
        for (var index = 0; index < 500; index++)
        {
            var candidate = Guid.NewGuid();
            if (store.ShardFor(candidate) == shard)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"no account mapped to shard {shard}");
    }

    private async Task<Guid> CreateSessionAsync(Guid accountId)
    {
        var store = _factory.Services.GetRequiredService<SessionStore>();
        var now = DateTimeOffset.UtcNow;
        var session = Nerves.Session.Domain.Session.Create(
            accountId,
            null,
            "nerves",
            "device",
            "iphash",
            "uahash",
            ["pwd"],
            now,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(12));

        await store.AddAsync(session, TestContext.Current.CancellationToken);
        return session.Id;
    }

    private static async Task<int> CountSessionsAsync(string connectionString, Guid accountId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "select count(*) from session.\"session\" where \"AccountId\" = @account", connection);
        command.Parameters.AddWithValue("account", accountId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<int> CountRevokedAsync(string connectionString, Guid sessionId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "select count(*) from session.\"session\" where \"Id\" = @id and \"RevokedAt\" is not null", connection);
        command.Parameters.AddWithValue("id", sessionId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
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

    private sealed record SessionListItem(Guid Id, bool Revoked);
}
