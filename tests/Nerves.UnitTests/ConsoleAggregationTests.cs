using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nerves.ConsoleApi.Aggregation;
using Nerves.ConsoleApi.Options;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class ConsoleAggregationTests
{
    private static readonly ConsoleOptions Options = new()
    {
        TimeoutSeconds = 1,
        MaxAuditEvents = 5,
        AccountScanLimit = 2,
        Services = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["identity"] = "http://identity/",
            ["session"] = "http://session/",
            ["authorization"] = "http://authorization/",
            ["audit"] = "http://audit/",
            ["compliance"] = "http://compliance/",
        },
    };

    [Fact]
    public async Task Overview_merges_the_downstream_payloads()
    {
        var client = Stub()
            .With("identity", "/api/v1/stats", Json("""{"accounts":42,"legalEntities":7}"""))
            .With("authorization", "/api/v1/decisions/stats", Json("""{"entries":3}"""))
            .With("compliance", "/api/v1/compliance/summary", Json("""{"consents":9}"""))
            .WithMeta("identity", "1.2.3")
            .WithMeta("session", "1.2.3")
            .WithMeta("authorization", "1.2.3")
            .WithMeta("audit", "1.2.3")
            .WithMeta("compliance", "1.2.3");

        var overview = await new ConsoleAggregator(client, Options).OverviewAsync(CancellationToken.None);

        overview.Warnings.ShouldBeEmpty();
        overview.Services.Count.ShouldBe(5);
        overview.Services.ShouldAllBe(status => status.Reachable);
        overview.Services.ShouldAllBe(status => status.Version == "1.2.3");
        overview.Identity!.Value.GetProperty("accounts").GetInt32().ShouldBe(42);
        overview.Compliance!.Value.GetProperty("consents").GetInt32().ShouldBe(9);
    }

    [Fact]
    public async Task Overview_degrades_instead_of_failing_when_a_dependency_is_down()
    {
        var client = Stub()
            .With("identity", "/api/v1/stats", DownstreamResult.Failed(0, "identity /api/v1/stats timed out after 1s", 1))
            .With("authorization", "/api/v1/decisions/stats", Json("""{"entries":3}"""))
            .With("compliance", "/api/v1/compliance/summary", Json("""{"consents":9}"""))
            .WithMeta("identity", "1.2.3")
            .WithMeta("session", "1.2.3")
            .WithMeta("authorization", "1.2.3")
            .WithMeta("audit", "1.2.3")
            .WithMeta("compliance", "1.2.3");

        var overview = await new ConsoleAggregator(client, Options).OverviewAsync(CancellationToken.None);

        overview.Identity.ShouldBeNull();
        overview.Authorization.ShouldNotBeNull();
        overview.Warnings.ShouldContain("identity /api/v1/stats timed out after 1s");
    }

    [Fact]
    public async Task Account_returns_null_when_identity_reports_not_found()
    {
        var id = Guid.CreateVersion7();
        var client = Stub()
            .With("identity", AccountPath(id), DownstreamResult.Failed(404, "identity answered HTTP 404", 1))
            .With("session", SessionsPath(id), Json("[]"))
            .With("audit", AuditPath(id), Json("""{"items":[]}"""));

        var account = await new ConsoleAggregator(client, Options).AccountAsync(id, CancellationToken.None);

        account.ShouldBeNull();
    }

    [Fact]
    public async Task Account_keeps_the_primary_payload_and_warns_about_the_rest()
    {
        var id = Guid.CreateVersion7();
        var client = Stub()
            .With("identity", AccountPath(id), Json("""{"id":"acct","username":"admin"}"""))
            .With("session", SessionsPath(id), DownstreamResult.Failed(500, "session answered HTTP 500", 1))
            .With("audit", AuditPath(id), Json("""{"items":[]}"""));

        var account = await new ConsoleAggregator(client, Options).AccountAsync(id, CancellationToken.None);

        account.ShouldNotBeNull();
        account.Account!.Value.GetProperty("username").GetString().ShouldBe("admin");
        account.Sessions.ShouldBeNull();
        account.Warnings.ShouldContain("session answered HTTP 500");
    }

    [Fact]
    public async Task Entity_keeps_only_the_accounts_that_belong_to_it_and_flags_truncation()
    {
        var entityId = Guid.CreateVersion7();
        var otherId = Guid.CreateVersion7();

        var page = JsonSerializer.Serialize(new
        {
            items = new object[]
            {
                new { id = Guid.CreateVersion7(), legalEntityId = entityId.ToString() },
                new { id = Guid.CreateVersion7(), legalEntityId = otherId.ToString() },
            },
            nextCursor = (string?)null,
        });

        var client = Stub()
            .With("identity", EntityPath(entityId), Json("""{"id":"entity","status":"active"}"""))
            .With("identity", "/api/v1/accounts?limit=2", Json(page));

        var entity = await new ConsoleAggregator(client, Options).EntityAsync(entityId, CancellationToken.None);

        entity.ShouldNotBeNull();
        entity.LegalEntity.ShouldNotBeNull();
        entity.Accounts.Count.ShouldBe(1);
        entity.AccountsTruncated.ShouldBeTrue();
        entity.Warnings.ShouldContain(warning => warning.Contains("raise Console:AccountScanLimit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Entity_returns_null_when_identity_reports_not_found()
    {
        var entityId = Guid.CreateVersion7();
        var client = Stub()
            .With("identity", EntityPath(entityId), DownstreamResult.Failed(404, "identity answered HTTP 404", 1))
            .With("identity", "/api/v1/accounts?limit=2", Json("""{"items":[]}"""));

        var entity = await new ConsoleAggregator(client, Options).EntityAsync(entityId, CancellationToken.None);

        entity.ShouldBeNull();
    }

    [Fact]
    public async Task The_downstream_health_check_degrades_when_one_service_is_down()
    {
        var client = Stub()
            .With("identity", "/healthz", Json("""{"status":"ok"}"""))
            .With("session", "/healthz", Json("""{"status":"ok"}"""))
            .With("authorization", "/healthz", DownstreamResult.Failed(0, "authorization is unreachable", 1))
            .With("audit", "/healthz", Json("""{"status":"ok"}"""))
            .With("compliance", "/healthz", Json("""{"status":"ok"}"""));

        var health = await new DownstreamHealthCheck(client, Options)
            .CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        health.Status.ShouldBe(HealthStatus.Degraded);
        health.Data["authorization"].ShouldBe("unreachable");
        health.Data["identity"].ShouldBe("ok");
    }

    private static string AccountPath(Guid id) => "/api/v1/accounts/" + id.ToString("D");

    private static string SessionsPath(Guid id) => "/api/v1/accounts/" + id.ToString("D") + "/sessions";

    private static string AuditPath(Guid id) => "/api/v1/audit-events?actorId=" + id.ToString("D") + "&limit=5";

    private static string EntityPath(Guid id) => "/api/v1/legal-entities/" + id.ToString("D");

    private static StubClient Stub() => new();

    private static DownstreamResult Json(string body)
    {
        using var document = JsonDocument.Parse(body);
        return new DownstreamResult(document.RootElement.Clone(), 200, null, 1);
    }

    private sealed class StubClient : IDownstreamClient
    {
        private readonly Dictionary<string, DownstreamResult> _results = new(StringComparer.Ordinal);

        public StubClient With(string service, string path, DownstreamResult result)
        {
            _results[Key(service, path)] = result;
            return this;
        }

        public StubClient WithMeta(string service, string version) =>
            With(
                service,
                "/api/v1/meta",
                Json(JsonSerializer.Serialize(new { service, version })));

        public Task<DownstreamResult> GetAsync(string service, string path, CancellationToken cancellationToken) =>
            Task.FromResult(_results.TryGetValue(Key(service, path), out var result)
                ? result
                : DownstreamResult.Failed(500, service + " " + path + " was not stubbed", 1));

        private static string Key(string service, string path) => service + " " + path;
    }
}
