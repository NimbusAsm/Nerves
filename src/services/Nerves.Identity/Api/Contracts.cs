namespace Nerves.Identity.Api;

/// <summary>Request body for creating a network account.</summary>
public sealed record CreateAccountRequest(
    Guid? LegalEntityId,
    string Realm,
    string Username,
    string? Email,
    string? PhoneNumber,
    string? DisplayName,
    string? Password);

/// <summary>Request body for creating a legal entity.</summary>
public sealed record CreateLegalEntityRequest(
    string Type,
    string DisplayName,
    string DocumentType,
    string DocumentHash,
    string Country);

/// <summary>Account representation returned by the management API.</summary>
public sealed record AccountResponse(
    string Id,
    string? LegalEntityId,
    string Realm,
    string Username,
    string? Email,
    string? DisplayName,
    string Status,
    string Type,
    DateTimeOffset CreatedAt);

/// <summary>Legal entity representation returned by the management API.</summary>
public sealed record LegalEntityResponse(
    string Id,
    string Type,
    string DisplayName,
    string VerificationState,
    DateTimeOffset CreatedAt);

/// <summary>Cursor page envelope.</summary>
public sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
