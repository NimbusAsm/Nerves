using System.Net;
using System.Text.Json;
using Shouldly;

namespace Nerves.Client.Tests;

public sealed class SessionsTests
{
    private static readonly Guid AccountId = Guid.Parse("0199a0c0-0000-7000-8000-000000000001");
    private static readonly Guid SessionId = Guid.Parse("0199a0c0-0000-7000-8000-000000000030");

    private const string SessionsJson =
        "[{\"id\":\"0199a0c0-0000-7000-8000-000000000030\",\"deviceId\":\"web-1\",\"createdAt\":\"2026-01-01T00:00:00Z\",\"lastSeenAt\":\"2026-01-02T00:00:00Z\",\"absoluteExpiresAt\":\"2026-02-01T00:00:00Z\",\"revoked\":false,\"revokedReason\":null}]";

    [Fact]
    public async Task List_reads_the_account_sessions_collection()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, SessionsJson);

        var sessions = await client.Sessions.List(AccountId).SendAsync();

        sessions.Single().DeviceId.ShouldBe("web-1");
        sessions.Single().Revoked.ShouldBeFalse();
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/api/v1/accounts/{AccountId:D}/sessions");
    }

    [Fact]
    public async Task Revoke_issues_a_delete_for_the_session()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK);

        await client.Sessions.Revoke(SessionId).SendAsync();

        var request = handler.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Delete);
        request.Uri.AbsolutePath.ShouldBe($"/api/v1/sessions/{SessionId:D}");
    }
}

public sealed class PermissionsTests
{
    private const string PermissionJson =
        "{\"id\":\"0199a0c0-0000-7000-8000-000000000040\",\"code\":\"accounts.read\",\"description\":\"Read accounts\"}";

    [Fact]
    public async Task List_reads_permission_codes()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, "[" + PermissionJson + "]");

        var permissions = await client.Permissions.List().SendAsync();

        permissions.Single().Code.ShouldBe("accounts.read");
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe("/api/v1/permissions");
    }

    [Fact]
    public async Task Create_posts_code_and_description_with_an_idempotency_key()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.Created, PermissionJson);

        var permission = await client.Permissions.Create()
            .WithCode("accounts.read")
            .WithDescription("Read accounts")
            .SendAsync();

        permission.Code.ShouldBe("accounts.read");
        var request = handler.Requests.Single();
        request.Header("Idempotency-Key").ShouldNotBeNullOrWhiteSpace();

        using var body = Json.Parse(request.Body);
        body.RootElement.GetProperty("code").GetString().ShouldBe("accounts.read");
        body.RootElement.GetProperty("description").GetString().ShouldBe("Read accounts");
    }

    [Fact]
    public void Create_requires_a_code()
    {
        var (client, _) = TestClient.Create();
        using var __ = client;

        Should.Throw<InvalidOperationException>(() => client.Permissions.Create().SendAsync());
    }
}

public sealed class GroupsTests
{
    private static readonly Guid GroupId = Guid.Parse("0199a0c0-0000-7000-8000-000000000050");

    private const string GroupJson =
        "{\"id\":\"0199a0c0-0000-7000-8000-000000000050\",\"realm\":\"default\",\"code\":\"ops\",\"name\":\"Operations\"}";

    [Fact]
    public async Task List_sends_the_realm_filter()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, "[" + GroupJson + "]");

        var groups = await client.Groups.List().InRealm("default").SendAsync();

        groups.Single().Code.ShouldBe("ops");
        Uri.UnescapeDataString(handler.Requests.Single().Uri.Query).ShouldContain("realm=default");
    }

    [Fact]
    public async Task Create_sends_realm_code_and_name()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.Created, GroupJson);

        await client.Groups.Create().InRealm("default").WithCode("ops").WithName("Operations").SendAsync();

        using var body = Json.Parse(handler.Requests.Single().Body);
        body.RootElement.GetProperty("realm").GetString().ShouldBe("default");
        body.RootElement.GetProperty("code").GetString().ShouldBe("ops");
        body.RootElement.GetProperty("name").GetString().ShouldBe("Operations");
    }

    [Fact]
    public async Task Get_reads_the_member_count()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK,
            "{\"id\":\"0199a0c0-0000-7000-8000-000000000050\",\"realm\":\"default\",\"code\":\"ops\",\"name\":\"Operations\",\"memberCount\":3}");

        var group = await client.Groups.Get(GroupId).SendAsync();

        group.MemberCount.ShouldBe(3);
    }

    [Fact]
    public async Task Members_are_listed_added_and_removed()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        var accountId = Guid.Parse("0199a0c0-0000-7000-8000-000000000001");
        handler
            .Respond(HttpStatusCode.OK,
                "[{\"accountId\":\"0199a0c0-0000-7000-8000-000000000001\",\"addedAt\":\"2026-01-01T00:00:00Z\"}]")
            .Respond(HttpStatusCode.Created)
            .Respond(HttpStatusCode.OK);

        var members = await client.Groups.Members(GroupId).SendAsync();
        await client.Groups.AddMember(GroupId).WithAccount(accountId).SendAsync();
        await client.Groups.RemoveMember(GroupId, accountId).SendAsync();

        members.Single().AccountId.ShouldBe(accountId);
        handler.Requests[1].Method.ShouldBe(HttpMethod.Post);
        using var body = Json.Parse(handler.Requests[1].Body);
        body.RootElement.GetProperty("accountId").GetString().ShouldBe(accountId.ToString("D"));
        handler.Requests[2].Method.ShouldBe(HttpMethod.Delete);
        handler.Requests[2].Uri.AbsolutePath.ShouldBe($"/api/v1/groups/{GroupId:D}/members/{accountId:D}");
    }
}

public sealed class DecisionsTests
{
    [Fact]
    public async Task Check_posts_the_permission_and_the_subject()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        var accountId = Guid.Parse("0199a0c0-0000-7000-8000-000000000001");
        handler.Respond(HttpStatusCode.OK,
            "{\"allowed\":true,\"source\":\"local\",\"authorizationVersion\":7,\"permission\":\"accounts.read\"}");

        var decision = await client.Decisions.Check("accounts.read").ForAccount(accountId).SendAsync();

        decision.Allowed.ShouldBeTrue();
        decision.AuthorizationVersion.ShouldBe(7);
        var request = handler.Requests.Single();
        request.Uri.AbsolutePath.ShouldBe("/api/v1/decisions/check");
        using var body = Json.Parse(request.Body);
        body.RootElement.GetProperty("permission").GetString().ShouldBe("accounts.read");
        body.RootElement.GetProperty("accountId").GetString().ShouldBe(accountId.ToString("D"));
    }

    [Fact]
    public async Task Stats_reads_cache_metrics()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, "{\"entries\":2,\"hits\":10,\"misses\":2,\"hitRate\":0.8333}");

        var stats = await client.Decisions.Stats().SendAsync();

        stats.Entries.ShouldBe(2);
        stats.HitRate.ShouldBe(0.8333);
    }
}
