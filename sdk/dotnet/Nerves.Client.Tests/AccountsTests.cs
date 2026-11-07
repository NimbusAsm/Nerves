using System.Net;
using System.Text.Json;
using Shouldly;

namespace Nerves.Client.Tests;

public sealed class AccountsTests
{
    private static readonly Guid EntityId = Guid.Parse("0199a0c0-0000-7000-8000-000000000010");
    private static readonly Guid AccountId = Guid.Parse("0199a0c0-0000-7000-8000-000000000001");

    private const string AccountJson = "{\"id\":\"0199a0c0-0000-7000-8000-000000000001\",\"legalEntityId\":\"0199a0c0-0000-7000-8000-000000000010\",\"realm\":\"default\",\"username\":\"alice\",\"email\":\"alice@example.com\",\"phoneNumber\":null,\"displayName\":null,\"type\":\"personal\",\"status\":\"active\",\"isPrimary\":false,\"roles\":[\"member\"],\"createdAt\":\"2026-01-01T00:00:00Z\",\"updatedAt\":\"2026-01-01T00:00:00Z\"}";

    [Fact]
    public async Task Create_sends_the_documented_request()
    {
        var (fresh, freshHandler) = TestClient.Create();
        freshHandler.Respond(HttpStatusCode.Created, AccountJson);

        var account = await fresh.Accounts
            .Create()
            .ForEntity(EntityId)
            .InRealm("default")
            .WithUsername("alice")
            .WithEmail("alice@example.com")
            .WithRoles("member", "admin")
            .WithPrimary()
            .SendAsync();

        account.Username.ShouldBe("alice");
        account.Type.ShouldBe(AccountType.Personal);
        account.Status.ShouldBe(AccountStatus.Active);
        account.Roles.ShouldBe(new[] { "member" });

        var request = freshHandler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.ToString().ShouldBe(TestClient.BaseUrl + "/api/v1/accounts");
        request.Header("Authorization").ShouldBe("Bearer " + TestClient.Token);
        request.Header("Idempotency-Key").ShouldNotBeNullOrWhiteSpace();

        using var body = Json.Parse(request.Body);
        body.RootElement.GetProperty("legalEntityId").GetString().ShouldBe(EntityId.ToString("D"));
        body.RootElement.GetProperty("realm").GetString().ShouldBe("default");
        body.RootElement.GetProperty("username").GetString().ShouldBe("alice");
        body.RootElement.GetProperty("email").GetString().ShouldBe("alice@example.com");
        body.RootElement.GetProperty("type").GetString().ShouldBe("personal");
        body.RootElement.GetProperty("isPrimary").GetBoolean().ShouldBeTrue();
        Json.Strings(body.RootElement, "roles").ShouldBe(new[] { "member", "admin" });
    }

    [Fact]
    public void Create_rejects_a_personal_account_without_an_entity()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;

        var exception = Should.Throw<InvalidOperationException>(() =>
            client.Accounts.Create().WithUsername("alice").SendAsync());

        exception.Message.ShouldContain("ForEntity");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_lets_service_accounts_omit_the_entity()
    {
        var (client, handler) = TestClient.Create();
        using var owned = client;
        handler.Respond(HttpStatusCode.Created, AccountJson);

        await client.Accounts
            .Create()
            .WithType(AccountType.Service)
            .InRealm("internal")
            .WithUsername("svc-billing")
            .SendAsync();

        using var body = Json.Parse(handler.Requests.Single().Body);
        body.RootElement.TryGetProperty("legalEntityId", out _).ShouldBeFalse();
        body.RootElement.GetProperty("type").GetString().ShouldBe("service");
    }

    [Fact]
    public async Task Builders_are_immutable_and_reusable()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler
            .Respond(HttpStatusCode.Created, AccountJson)
            .Respond(HttpStatusCode.Created, AccountJson);

        var baseBuilder = client.Accounts.Create().ForEntity(EntityId).WithUsername("alice");

        await baseBuilder.WithEmail("first@example.com").SendAsync();
        await baseBuilder.WithEmail("second@example.com").SendAsync();

        using var first = Json.Parse(handler.Requests[0].Body);
        using var second = Json.Parse(handler.Requests[1].Body);

        first.RootElement.GetProperty("email").GetString().ShouldBe("first@example.com");
        second.RootElement.GetProperty("email").GetString().ShouldBe("second@example.com");
        first.RootElement.GetProperty("username").GetString().ShouldBe("alice");
    }

    [Fact]
    public async Task Get_reads_one_account_by_id()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, AccountJson);

        var account = await client.Accounts.Get(AccountId).SendAsync();

        account.Id.ShouldBe(AccountId);
        handler.Requests.Single().Method.ShouldBe(HttpMethod.Get);
        handler.Requests.Single().Uri.ToString().ShouldBe(TestClient.BaseUrl + "/api/v1/accounts/" + AccountId.ToString("D"));
    }

    [Fact]
    public async Task List_builds_a_query_and_returns_the_next_cursor()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, "{\"items\":[" + AccountJson + "],\"nextCursor\":\"cursor-2\"}");

        var page = await client.Accounts
            .List()
            .ForEntity(EntityId)
            .InRealm("default")
            .WithStatus(AccountStatus.Active)
            .Filter("username", "alice")
            .Sort("-createdAt")
            .Limit(2)
            .SendAsync();

        page.Items.Count.ShouldBe(1);
        page.NextCursor.ShouldBe("cursor-2");
        page.HasMore.ShouldBeTrue();

        var query = Uri.UnescapeDataString(handler.Requests.Single().Uri.Query);
        query.ShouldContain("legalEntityId=" + EntityId.ToString("D"));
        query.ShouldContain("realm=default");
        query.ShouldContain("filter[status]=active");
        query.ShouldContain("filter[username]=alice");
        query.ShouldContain("sort=-createdAt");
        query.ShouldContain("limit=2");
    }

    [Fact]
    public async Task List_stream_follows_cursors_until_exhausted()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler
            .Respond(HttpStatusCode.OK, "{\"items\":[" + AccountJson + "],\"nextCursor\":\"cursor-2\"}")
            .Respond(HttpStatusCode.OK, "{\"items\":[" + AccountJson + "],\"nextCursor\":null}");

        var usernames = new List<string>();
        await foreach (var account in client.Accounts.List().ForEntity(EntityId).Limit(1).StreamAsync())
        {
            usernames.Add(account.Username);
        }

        usernames.Count.ShouldBe(2);
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[0].Uri.Query.ShouldNotContain("cursor=");
        Uri.UnescapeDataString(handler.Requests[1].Uri.Query).ShouldContain("cursor=cursor-2");
    }

    [Fact]
    public async Task Update_sends_only_the_changed_fields_with_if_match()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, AccountJson);

        await client.Accounts
            .Update(AccountId)
            .WithDisplayName("Alice A.")
            .IfMatch("\"v1\"")
            .SendAsync();

        var request = handler.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Patch);
        request.Header("If-Match").ShouldBe("\"v1\"");

        using var body = Json.Parse(request.Body);
        body.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(new[] { "displayName" });
        body.RootElement.GetProperty("displayName").GetString().ShouldBe("Alice A.");
    }

    [Fact]
    public async Task Update_can_clear_optional_fields()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, AccountJson);

        await client.Accounts.Update(AccountId).WithEmail(null).SendAsync();

        using var body = Json.Parse(handler.Requests.Single().Body);
        body.RootElement.TryGetProperty("email", out var email).ShouldBeTrue();
        email.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Update_can_replace_roles()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, AccountJson);

        await client.Accounts.Update(AccountId).WithRoles("admin", "ops").SendAsync();

        using var body = Json.Parse(handler.Requests.Single().Body);
        Json.Strings(body.RootElement, "roles").ShouldBe(new[] { "admin", "ops" });
    }

    [Fact]
    public void Update_rejects_an_empty_patch()
    {
        var (client, _) = TestClient.Create();
        using var __ = client;

        Should.Throw<InvalidOperationException>(() => client.Accounts.Update(AccountId).SendAsync());
    }

    [Fact]
    public async Task Delete_accepts_no_content()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.NoContent);

        await client.Accounts.Delete(AccountId).IfMatch("\"v1\"").SendAsync();

        var request = handler.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Delete);
        request.Header("If-Match").ShouldBe("\"v1\"");
        request.Uri.AbsolutePath.ShouldBe("/api/v1/accounts/" + AccountId.ToString("D"));
    }

    [Fact]
    public async Task Problem_details_become_NervesApiException()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.RespondWith(_ => TestClient.Problem(
            HttpStatusCode.Conflict,
            "{\"type\":\"https://nerves.dev/problems/conflict\",\"title\":\"Conflict\",\"status\":409,\"detail\":\"username already exists\",\"traceId\":\"trace-42\"}"));

        var exception = await Should.ThrowAsync<NervesApiException>(() =>
            client.Accounts.Create().ForEntity(EntityId).WithUsername("alice").SendAsync());

        exception.Status.ShouldBe(409);
        exception.Type.ShouldBe("https://nerves.dev/problems/conflict");
        exception.TraceId.ShouldBe("trace-42");
        exception.Detail.ShouldBe("username already exists");
        exception.Problem.Status.ShouldBe(409);
    }

    [Fact]
    public async Task Non_json_errors_still_carry_status_and_trace_header()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.RespondWith(_ => TestClient.Problem(HttpStatusCode.BadGateway, "upstream unavailable", "trace-proxy"));

        var exception = await Should.ThrowAsync<NervesApiException>(() => client.Accounts.Get(AccountId).SendAsync());

        exception.Status.ShouldBe(502);
        exception.TraceId.ShouldBe("trace-proxy");
        exception.RawBody.ShouldBe("upstream unavailable");
    }

    [Fact]
    public async Task Retries_transient_failures_with_the_same_idempotency_key()
    {
        var (client, handler) = TestClient.Create(maxRetries: 1);
        using var _ = client;
        handler
            .Respond(HttpStatusCode.ServiceUnavailable)
            .Respond(HttpStatusCode.Created, AccountJson);

        await client.Accounts.Create().ForEntity(EntityId).WithUsername("alice").SendAsync();

        handler.Requests.Count.ShouldBe(2);
        handler.Requests[0].Header("Idempotency-Key").ShouldBe(handler.Requests[1].Header("Idempotency-Key"));
    }
}
