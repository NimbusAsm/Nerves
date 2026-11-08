using System.Text.Json.Serialization;

namespace Nerves.Identity.Scim;

/// <summary>SCIM 2.0 User resource (RFC 7643 subset).</summary>
public sealed record ScimUser
{
    public string[] Schemas { get; init; } = ["urn:ietf:params:scim:schemas:core:2.0:User"];

    public string Id { get; init; } = string.Empty;

    public string? ExternalId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public ScimName? Name { get; init; }

    public ScimEmail[] Emails { get; init; } = [];

    public string? DisplayName { get; init; }

    public bool Active { get; init; }

    public ScimMeta Meta { get; init; } = new();
}

public sealed record ScimName(string? Formatted);

public sealed record ScimEmail(string Value, string Type = "work", bool Primary = true);

public sealed record ScimMeta
{
    [JsonPropertyName("resourceType")] public string ResourceType { get; init; } = "User";

    public DateTimeOffset? Created { get; init; }

    public DateTimeOffset? LastModified { get; init; }
}

/// <summary>SCIM list envelope.</summary>
public sealed record ScimListResponse<T>
{
    public string[] Schemas { get; init; } = ["urn:ietf:params:scim:api:messages:2.0:ListResponse"];

    public int TotalResults { get; init; }

    public int StartIndex { get; init; } = 1;

    public int ItemsPerPage { get; init; }

    public T[] Resources { get; init; } = [];
}

/// <summary>SCIM error envelope.</summary>
public sealed record ScimError
{
    public string[] Schemas { get; init; } = ["urn:ietf:params:scim:api:messages:2.0:Error"];

    public string Detail { get; init; } = string.Empty;

    public string? ScimType { get; init; }

    public int Status { get; init; }
}
