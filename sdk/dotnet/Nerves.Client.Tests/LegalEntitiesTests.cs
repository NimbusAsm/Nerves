using System.Net;
using System.Text.Json;
using Shouldly;

namespace Nerves.Client.Tests;

public sealed class LegalEntitiesTests
{
    private static readonly Guid EntityId = Guid.Parse("0199a0c0-0000-7000-8000-000000000010");

    private const string EntityJson = "{\"id\":\"0199a0c0-0000-7000-8000-000000000010\",\"type\":\"naturalPerson\",\"displayName\":\"Alice\",\"realName\":{\"documentType\":\"passport\",\"country\":\"CN\"},\"verificationState\":\"pending\",\"verifiedAt\":null,\"providerReference\":null,\"createdAt\":\"2026-01-01T00:00:00Z\",\"updatedAt\":\"2026-01-01T00:00:00Z\"}";

    [Fact]
    public async Task Create_sends_the_document_once_and_maps_the_response()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.Created, EntityJson);

        var entity = await client.LegalEntities
            .Create()
            .WithType(LegalEntityType.NaturalPerson)
            .WithDisplayName("Alice")
            .WithDocument(IdDocumentType.Passport, "E12345678", "CN")
            .SendAsync();

        entity.Id.ShouldBe(EntityId);
        entity.Type.ShouldBe(LegalEntityType.NaturalPerson);
        entity.VerificationState.ShouldBe(VerificationState.Pending);
        entity.RealName!.DocumentType.ShouldBe(IdDocumentType.Passport);
        entity.RealName.Country.ShouldBe("CN");

        var request = handler.Requests.Single();
        request.Uri.AbsolutePath.ShouldBe("/api/v1/legal-entities");

        using var body = Json.Parse(request.Body);
        body.RootElement.GetProperty("displayName").GetString().ShouldBe("Alice");
        body.RootElement.GetProperty("type").GetString().ShouldBe("naturalPerson");
        var realName = body.RootElement.GetProperty("realName");
        realName.GetProperty("documentType").GetString().ShouldBe("passport");
        realName.GetProperty("number").GetString().ShouldBe("E12345678");
        realName.GetProperty("country").GetString().ShouldBe("CN");
    }

    [Fact]
    public void Create_requires_a_document()
    {
        var (client, _) = TestClient.Create();
        using var __ = client;

        Should.Throw<InvalidOperationException>(() =>
            client.LegalEntities.Create().WithDisplayName("Alice").SendAsync());
    }

    [Fact]
    public async Task List_filters_by_state_and_pages_by_cursor()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, "{\"items\":[" + EntityJson + "],\"nextCursor\":\"next\"}");

        var page = await client.LegalEntities
            .List()
            .WithVerificationState(VerificationState.Verified)
            .WithType(LegalEntityType.LegalPerson)
            .Sort("displayName")
            .Limit(10)
            .SendAsync();

        page.NextCursor.ShouldBe("next");
        var query = Uri.UnescapeDataString(handler.Requests.Single().Uri.Query);
        query.ShouldContain("filter[verificationState]=verified");
        query.ShouldContain("filter[type]=legalPerson");
        query.ShouldContain("sort=displayName");
        query.ShouldContain("limit=10");
    }

    [Fact]
    public async Task Update_patches_verification_state_with_if_match()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, EntityJson);

        await client.LegalEntities
            .Update(EntityId)
            .WithVerificationState(VerificationState.Verified)
            .WithProviderReference("provider-7")
            .IfMatch("\"v2\"")
            .SendAsync();

        var request = handler.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Patch);
        request.Header("If-Match").ShouldBe("\"v2\"");

        using var body = Json.Parse(request.Body);
        body.RootElement.GetProperty("verificationState").GetString().ShouldBe("verified");
        body.RootElement.GetProperty("providerReference").GetString().ShouldBe("provider-7");
    }

    [Fact]
    public async Task Delete_targets_the_legal_entities_path()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.NoContent);

        await client.LegalEntities.Delete(EntityId).SendAsync();

        handler.Requests.Single().Uri.AbsolutePath.ShouldBe("/api/v1/legal-entities/" + EntityId.ToString("D"));
    }
}
