using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nerves.Session.Domain;
using Nerves.Session.Persistence;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Nerves.Session.IntegrationTests;

/// <summary>Exercises the session management API against a real PostgreSQL container.</summary>
public sealed class SessionApiTests : IAsyncLifetime
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
        _client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.PermissionHeader, "nerves:admin");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SessionDbContext>();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Rejects_requests_without_a_token()
    {
        using var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync("/api/v1/accounts/" + Guid.NewGuid() + "/sessions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Lists_and_revokes_a_session()
    {
        var accountId = Guid.NewGuid();
        var sessionId = await SeedSessionAsync(accountId);

        var listed = await _client.GetAsync($"/api/v1/accounts/{accountId}/sessions", TestContext.Current.CancellationToken);
        listed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await listed.Content.ReadFromJsonAsync<List<SessionListItem>>(TestContext.Current.CancellationToken);
        payload.ShouldNotBeNull();
        payload.ShouldHaveSingleItem().Revoked.ShouldBeFalse();

        var revoked = await _client.DeleteAsync($"/api/v1/sessions/{sessionId}", TestContext.Current.CancellationToken);
        revoked.IsSuccessStatusCode.ShouldBeTrue();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SessionDbContext>();
        var stored = await db.Sessions.AsNoTracking().SingleAsync(x => x.Id == sessionId, TestContext.Current.CancellationToken);
        stored.RevokedAt.ShouldNotBeNull();

        // The audit row is written in the same request as the revocation.
        var outbox = await db.OutboxEvents.AsNoTracking()
            .Where(x => x.Type == "session.admin_revoked")
            .ToListAsync(TestContext.Current.CancellationToken);
        outbox.ShouldNotBeEmpty();
        outbox.ShouldAllBe(x => x.DispatchedAt == null);
        outbox.ShouldAllBe(x => x.TargetId == sessionId.ToString());
    }

    private async Task<Guid> SeedSessionAsync(Guid accountId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SessionDbContext>();
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

        db.Sessions.Add(session);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return session.Id;
    }

    private sealed record SessionListItem(Guid Id, bool Revoked);
}
