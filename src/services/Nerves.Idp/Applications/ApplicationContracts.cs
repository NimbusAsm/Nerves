namespace Nerves.Idp.Applications;

/// <summary>Request body for creating an OIDC application.</summary>
public sealed record CreateApplicationRequest(
    string? ClientId,
    string? DisplayName,
    string? Description,
    string? LogoUrl,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris,
    IReadOnlyList<string>? Scopes,
    bool? Enabled);

/// <summary>Partial update; omitted fields stay unchanged.</summary>
public sealed record UpdateApplicationRequest(
    string? DisplayName,
    string? Description,
    string? LogoUrl,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris,
    IReadOnlyList<string>? Scopes,
    bool? Enabled);

/// <summary>Application representation returned by the management API.</summary>
public sealed record ApplicationResponse(
    string Id,
    string ClientId,
    string DisplayName,
    string Description,
    string? LogoUrl,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes,
    string Status,
    bool BuiltIn,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Cursor page envelope, matching the other management APIs.</summary>
public sealed record ApplicationPageResponse(IReadOnlyList<ApplicationResponse> Items, string? NextCursor);

/// <summary>A deleted application, so callers can confirm what disappeared.</summary>
public sealed record DeleteApplicationResponse(string Id, string ClientId, string DisplayName);
