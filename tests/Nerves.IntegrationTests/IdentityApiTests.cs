using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nerves.Identity.Persistence;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Nerves.IntegrationTests;

/// <summary>
/// Boots the identity service against a real PostgreSQL container so the
/// routing index, credentials and migrations are exercised end to end.
/// </summary>
public sealed class IdentityApiTests : IAsyncLifetime
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
            builder.UseSetting("Api:AdminToken", "integration-token");
            builder.UseSetting("Security:IdentifierHashKey", Convert.ToBase64String(new byte[32]));
            builder.UseSetting("Kestrel:Endpoints", string.Empty);
        });

        _client = _factory.CreateClient();

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
    public async Task Rejects_requests_without_a_token()
    {
        var response = await _client.GetAsync("/api/v1/stats", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Creates_legal_entity_and_account_and_blocks_duplicates()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "integration-token");

        var entityResponse = await _client.PostAsJsonAsync("/api/v1/legal-entities", new
        {
            type = "LegalPerson",
            displayName = "Integration Ltd",
            documentType = "UnifiedSocialCreditCode",
            documentHash = new string('c', 64),
            country = "CN",
        }, TestContext.Current.CancellationToken);

        entityResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entity = await entityResponse.Content.ReadFromJsonAsync<EntityDto>(TestContext.Current.CancellationToken);
        entity.ShouldNotBeNull();

        var accountResponse = await _client.PostAsJsonAsync("/api/v1/accounts", new
        {
            legalEntityId = entity!.Id,
            realm = "nerves",
            username = "integration-user",
            email = "integration@acme.test",
            displayName = "Integration User",
            password = "Str0ng!Pass",
        }, TestContext.Current.CancellationToken);

        accountResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var duplicate = await _client.PostAsJsonAsync("/api/v1/accounts", new
        {
            legalEntityId = entity.Id,
            realm = "nerves",
            username = "integration-user",
            email = "other@acme.test",
            password = "Str0ng!Pass",
        }, TestContext.Current.CancellationToken);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var list = await _client.GetAsync("/api/v1/accounts?limit=10", TestContext.Current.CancellationToken);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stats = await _client.GetFromJsonAsync<StatsDto>("/api/v1/stats", TestContext.Current.CancellationToken);
        stats.ShouldNotBeNull();
        stats!.Accounts.ShouldBe(1);
        stats.LegalEntities.ShouldBe(1);
    }

    private sealed record EntityDto(string Id);

    private sealed record StatsDto(int Accounts, int LegalEntities);
}
