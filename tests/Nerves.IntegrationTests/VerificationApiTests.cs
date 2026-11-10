using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nerves.Identity.Persistence;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Nerves.IntegrationTests;

/// <summary>
/// Exercises the real-name verification provider flow against a real
/// PostgreSQL container: start, status, callback, idempotency and outbox fan-out.
/// </summary>
public sealed class VerificationApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6-alpine").Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
            builder.UseSetting("DevSeed:Enabled", "false");
            builder.UseSetting("Kestrel:Endpoints", string.Empty);
            builder.UseSetting(
                "DataProtection:KeysDirectory",
                Path.Combine(Path.GetTempPath(), "nerves-it-keys-" + Guid.NewGuid().ToString("N")));
            builder.UseSetting("Security:IdentifierHashKey", Convert.ToBase64String(new byte[32]));
            builder.UseSetting("Verification:Provider", "manual");
            builder.UseSetting("Verification:PublicBaseUrl", "https://nerves.test");
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

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Start_records_an_attempt_and_keeps_the_entity_pending()
    {
        var entityId = await SeedEntityAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/legal-entities/{entityId}/verification/start",
            new { provider = "manual" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var status = await _client.GetFromJsonAsync<VerificationDto>(
            $"/api/v1/legal-entities/{entityId}/verification", TestContext.Current.CancellationToken);

        status.ShouldNotBeNull();
        status!.State.ShouldBe("Pending");
        status.Attempt.ShouldNotBeNull();
        status.Attempt!.Provider.ShouldBe("manual");
        status.Attempt.Status.ShouldBe("Pending");
    }

    [Fact]
    public async Task Start_is_blocked_for_an_unknown_provider()
    {
        var entityId = await SeedEntityAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/legal-entities/{entityId}/verification/start",
            new { provider = "does-not-exist" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_is_blocked_for_an_already_verified_entity()
    {
        var entityId = await SeedEntityAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var entity = await db.LegalEntities.SingleAsync(x => x.Id == entityId, TestContext.Current.CancellationToken);
            entity.MarkVerified("test", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/legal-entities/{entityId}/verification/start",
            new { provider = "manual" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_callback_is_idempotent_and_fans_out_one_event_per_account()
    {
        var entityId = await SeedEntityAsync();
        var accountId = await SeedAccountForEntityAsync(entityId);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var attempt = Nerves.Identity.Domain.VerificationAttempt
                .Create(entityId, "http", "provider-ref-1", DateTimeOffset.UtcNow).Value;
            db.VerificationAttempts.Add(attempt);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var first = await _client.PostAsJsonAsync(
            $"/api/v1/legal-entities/{entityId}/verification/callback",
            new { reference = "provider-ref-1", status = "verified", message = "ok" },
            TestContext.Current.CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var replay = await _client.PostAsJsonAsync(
            $"/api/v1/legal-entities/{entityId}/verification/callback",
            new { reference = "provider-ref-1", status = "verified" },
            TestContext.Current.CancellationToken);
        replay.StatusCode.ShouldBe(HttpStatusCode.OK);

        var conflicting = await _client.PostAsJsonAsync(
            $"/api/v1/legal-entities/{entityId}/verification/callback",
            new { reference = "provider-ref-1", status = "rejected" },
            TestContext.Current.CancellationToken);
        conflicting.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var entity = await verifyDb.LegalEntities.AsNoTracking()
            .SingleAsync(x => x.Id == entityId, TestContext.Current.CancellationToken);
        entity.VerificationState.ShouldBe(Nerves.Identity.Domain.VerificationState.Verified);

        var events = await verifyDb.OutboxEvents.AsNoTracking()
            .Where(x => x.LegalEntityId == entityId && x.AccountId == accountId)
            .ToListAsync(TestContext.Current.CancellationToken);
        events.ShouldHaveSingleItem().Type.ShouldBe("realname.changed");
    }

    [Fact]
    public async Task An_unknown_reference_is_rejected_without_touching_the_entity()
    {
        var entityId = await SeedEntityAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/legal-entities/{entityId}/verification/callback",
            new { reference = "unknown", status = "verified" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_invalid_callback_status_is_rejected()
    {
        var entityId = await SeedEntityAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/legal-entities/{entityId}/verification/callback",
            new { reference = "r", status = "pending" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> SeedEntityAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var now = DateTimeOffset.UtcNow;
        var realName = Nerves.Identity.Domain.RealNameId.Create(
            Nerves.Identity.Domain.IdDocumentType.UnifiedSocialCreditCode,
            Guid.NewGuid().ToString("N") + new string('0', 32),
            "CN").Value;
        var entity = Nerves.Identity.Domain.LegalEntity.Create(
            Nerves.Identity.Domain.LegalEntityType.LegalPerson, "Integration Ltd", realName, now).Value;

        db.LegalEntities.Add(entity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity.Id;
    }

    private async Task<Guid> SeedAccountForEntityAsync(Guid entityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<Nerves.Identity.Security.IPasswordHasher>();
        var identifiers = scope.ServiceProvider.GetRequiredService<Nerves.Identity.Security.IIdentifierHasher>();
        var shards = scope.ServiceProvider.GetRequiredService<Nerves.Identity.Sharding.ShardResolver>();

        var now = DateTimeOffset.UtcNow;
        var username = "user-" + Guid.NewGuid().ToString("N")[..8];
        var email = username + "@acme.test";

        var account = Nerves.Identity.Domain.NetworkAccount.Create(
            entityId, "nerves", username, email, null, username,
            Nerves.Identity.Domain.AccountType.Personal, now).Value;
        account.MarkPrimary();

        var credential = Nerves.Identity.Domain.AccountCredential.Create(
            account.Id, Nerves.Identity.Domain.CredentialType.Password, hasher.Hash("Old!Passw0rd123"),
            Nerves.Identity.Security.Pbkdf2PasswordHasher.Algorithm, now).Value;

        db.NetworkAccounts.Add(account);
        db.AccountCredentials.Add(credential);

        foreach (var (type, value) in new[] { ("username", username), ("email", email) })
        {
            db.RoutingIndex.Add(Nerves.Identity.Domain.RoutingIndexEntry.Create(
                identifiers.Hash(type, "nerves", value), type, "nerves", account.Id, entityId,
                shards.Resolve(account.Id), now));
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return account.Id;
    }

    private sealed record VerificationDto(string State, AttemptDto? Attempt);

    private sealed record AttemptDto(string Provider, string Status, string? Reference);
}
