using System.Net;
using System.Text.Json;
using Shouldly;

namespace Nerves.Client.Tests;

public sealed class RoleAssignmentTests
{
    private static readonly Guid RoleId = Guid.Parse("0199a0c0-0000-7000-8000-000000000020");
    private static readonly Guid AccountId = Guid.Parse("0199a0c0-0000-7000-8000-000000000001");

    [Fact]
    public async Task Assign_posts_the_subject()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK);

        await client.Roles.Assign(RoleId).WithSubject("account", AccountId).SendAsync();

        var request = handler.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe($"/api/v1/roles/{RoleId:D}/assign");
        using var body = Json.Parse(request.Body);
        body.RootElement.GetProperty("subjectType").GetString().ShouldBe("account");
        body.RootElement.GetProperty("subjectId").GetString().ShouldBe(AccountId.ToString("D"));
    }

    [Fact]
    public async Task Revoke_posts_the_subject()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK);

        await client.Roles.Revoke(RoleId).WithSubject("group", AccountId).SendAsync();

        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/api/v1/roles/{RoleId:D}/revoke");
    }

    [Fact]
    public void Assign_requires_a_subject()
    {
        var (client, _) = TestClient.Create();
        using var __ = client;

        Should.Throw<InvalidOperationException>(() => client.Roles.Assign(RoleId).SendAsync());
    }
}
