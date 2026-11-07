using System.Net;
using System.Text.Json;
using Shouldly;

namespace Nerves.Client.Tests;

public sealed class RolesTests
{
    private static readonly Guid RoleId = Guid.Parse("0199a0c0-0000-7000-8000-000000000020");

    private const string RoleJson = "{\"id\":\"0199a0c0-0000-7000-8000-000000000020\",\"name\":\"member\",\"description\":\"Members\",\"permissions\":[\"accounts.read\"],\"createdAt\":\"2026-01-01T00:00:00Z\",\"updatedAt\":\"2026-01-01T00:00:00Z\"}";

    [Fact]
    public async Task Create_sends_name_description_and_permissions()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.Created, RoleJson);

        var role = await client.Roles
            .Create()
            .WithName("member")
            .WithDescription("Members")
            .WithPermissions("accounts.read", "accounts.list")
            .SendAsync();

        role.Name.ShouldBe("member");
        role.Permissions.ShouldBe(new[] { "accounts.read" });

        var request = handler.Requests.Single();
        request.Uri.AbsolutePath.ShouldBe("/api/v1/roles");
        request.Header("Idempotency-Key").ShouldNotBeNullOrWhiteSpace();

        using var body = Json.Parse(request.Body);
        body.RootElement.GetProperty("name").GetString().ShouldBe("member");
        Json.Strings(body.RootElement, "permissions").ShouldBe(new[] { "accounts.read", "accounts.list" });
    }

    [Fact]
    public async Task Get_uses_the_role_id_path()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, RoleJson);

        var role = await client.Roles.Get(RoleId).SendAsync();

        role.Id.ShouldBe(RoleId);
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe("/api/v1/roles/" + RoleId.ToString("D"));
    }

    [Fact]
    public async Task List_supports_filter_sort_limit_and_cursor()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, "{\"items\":[" + RoleJson + "],\"nextCursor\":\"c1\"}");

        var page = await client.Roles
            .List()
            .Filter("name", "member")
            .Sort("name")
            .Limit(1)
            .Cursor("start")
            .SendAsync();

        page.Items.Single().Name.ShouldBe("member");
        page.NextCursor.ShouldBe("c1");

        var query = Uri.UnescapeDataString(handler.Requests.Single().Uri.Query);
        query.ShouldContain("filter[name]=member");
        query.ShouldContain("sort=name");
        query.ShouldContain("limit=1");
        query.ShouldContain("cursor=start");
    }

    [Fact]
    public async Task Stream_walks_every_page()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler
            .Respond(HttpStatusCode.OK, "{\"items\":[" + RoleJson + "],\"nextCursor\":\"c2\"}")
            .Respond(HttpStatusCode.OK, "{\"items\":[" + RoleJson + "],\"nextCursor\":null}");

        var ids = new List<Guid>();
        await foreach (var role in client.Roles.List().StreamAsync())
        {
            ids.Add(role.Id);
        }

        ids.Count.ShouldBe(2);
        Uri.UnescapeDataString(handler.Requests[1].Uri.Query).ShouldContain("cursor=c2");
    }

    [Fact]
    public async Task Update_replaces_permissions_and_delete_is_empty()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler
            .Respond(HttpStatusCode.OK, RoleJson)
            .Respond(HttpStatusCode.NoContent);

        await client.Roles.Update(RoleId).WithPermissions("roles.read").SendAsync();
        await client.Roles.Delete(RoleId).SendAsync();

        using var body = Json.Parse(handler.Requests[0].Body);
        Json.Strings(body.RootElement, "permissions").ShouldBe(new[] { "roles.read" });
        handler.Requests[1].Method.ShouldBe(HttpMethod.Delete);
    }

    [Fact]
    public void Create_requires_a_name()
    {
        var (client, _) = TestClient.Create();
        using var __ = client;

        Should.Throw<InvalidOperationException>(() => client.Roles.Create().SendAsync());
    }
}
