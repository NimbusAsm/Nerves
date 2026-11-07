namespace Nerves.Client;

/// <summary>What the network account is used for.</summary>
public enum AccountType
{
    /// <summary>A real person.</summary>
    Personal,

    /// <summary>A machine identity without a legal entity.</summary>
    Service,

    /// <summary>An account operated on behalf of someone else.</summary>
    Managed,
}

/// <summary>Account lifecycle.</summary>
public enum AccountStatus
{
    /// <summary>Usable.</summary>
    Active,

    /// <summary>Temporarily locked.</summary>
    Locked,

    /// <summary>Permanently disabled.</summary>
    Disabled,
}

/// <summary>Kind of legal entity.</summary>
public enum LegalEntityType
{
    /// <summary>Natural person.</summary>
    NaturalPerson,

    /// <summary>Company or other organisation.</summary>
    LegalPerson,

    /// <summary>Government body.</summary>
    Government,

    /// <summary>Anything else.</summary>
    Other,
}

/// <summary>Kind of identity document.</summary>
public enum IdDocumentType
{
    /// <summary>Resident identity card.</summary>
    ResidentIdentityCard,

    /// <summary>Passport.</summary>
    Passport,

    /// <summary>Unified social credit code.</summary>
    UnifiedSocialCreditCode,

    /// <summary>Anything else.</summary>
    Other,
}

/// <summary>Real-name verification lifecycle.</summary>
public enum VerificationState
{
    /// <summary>Waiting for a provider decision.</summary>
    Pending,

    /// <summary>Verified.</summary>
    Verified,

    /// <summary>Rejected by the provider.</summary>
    Rejected,

    /// <summary>Verification has expired.</summary>
    Expired,
}

/// <summary>A login identity returned by <c>/api/v1/accounts</c>.</summary>
public sealed record Account
{
    /// <summary>Server-assigned identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Owning legal entity; <see langword="null"/> only for service accounts.</summary>
    public Guid? LegalEntityId { get; init; }

    /// <summary>Realm the account lives in.</summary>
    public string Realm { get; init; } = string.Empty;

    /// <summary>Login name, unique within the realm.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Email address.</summary>
    public string? Email { get; init; }

    /// <summary>Phone number.</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Account type.</summary>
    public AccountType Type { get; init; }

    /// <summary>Account status.</summary>
    public AccountStatus Status { get; init; }

    /// <summary>Whether this is the entity's primary account.</summary>
    public bool IsPrimary { get; init; }

    /// <summary>Role names granted to the account.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the account was disabled, if it was.</summary>
    public DateTimeOffset? DisabledAt { get; init; }

    /// <summary>Last modification timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>The non-secret part of a legal entity's real-name identity.</summary>
public sealed record RealName
{
    /// <summary>Document type.</summary>
    public IdDocumentType DocumentType { get; init; }

    /// <summary>ISO 3166-1 alpha-2 issuing country.</summary>
    public string Country { get; init; } = string.Empty;
}

/// <summary>A verified subject returned by <c>/api/v1/legal-entities</c>.</summary>
public sealed record LegalEntity
{
    /// <summary>Server-assigned identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Entity type.</summary>
    public LegalEntityType Type { get; init; }

    /// <summary>Display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Document metadata; the document number itself is never returned.</summary>
    public RealName? RealName { get; init; }

    /// <summary>Verification lifecycle state.</summary>
    public VerificationState VerificationState { get; init; }

    /// <summary>When verification succeeded.</summary>
    public DateTimeOffset? VerifiedAt { get; init; }

    /// <summary>Opaque reference issued by the verification provider.</summary>
    public string? ProviderReference { get; init; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>A role returned by <c>/api/v1/roles</c>.</summary>
public sealed record Role
{
    /// <summary>Server-assigned identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Unique role name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Human-readable description.</summary>
    public string? Description { get; init; }

    /// <summary>Permission codes granted by the role.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
