namespace Nerves.Client;

/// <summary>A data-subject export document returned by <c>/compliance/subjects/{id}/export</c>.</summary>
public sealed record SubjectExport
{
    /// <summary>Data subject identifier.</summary>
    public string SubjectId { get; init; } = string.Empty;

    /// <summary>When the export was produced.</summary>
    public DateTimeOffset ExportedAt { get; init; }

    /// <summary>Account snapshot, when the service could resolve one.</summary>
    public SubjectExportAccount? Account { get; init; }

    /// <summary>Consent records for the subject.</summary>
    public IReadOnlyList<SubjectExportConsent> Consents { get; init; } = [];

    /// <summary>Retention policies in force.</summary>
    public IReadOnlyList<SubjectExportPolicy> RetentionPolicies { get; init; } = [];
}

/// <summary>Account snapshot inside a subject export.</summary>
public sealed record SubjectExportAccount
{
    /// <summary>Account id.</summary>
    public Guid AccountId { get; init; }

    /// <summary>Realm.</summary>
    public string Realm { get; init; } = string.Empty;

    /// <summary>Login name.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Account status.</summary>
    public string Status { get; init; } = string.Empty;
}

/// <summary>Consent entry inside a subject export.</summary>
public sealed record SubjectExportConsent
{
    /// <summary>Processing purpose.</summary>
    public string Purpose { get; init; } = string.Empty;

    /// <summary>Accepted consent-text version.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Grant timestamp.</summary>
    public DateTimeOffset GrantedAt { get; init; }

    /// <summary>Expiry timestamp.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Revocation timestamp.</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>Revocation reason.</summary>
    public string? RevocationReason { get; init; }
}

/// <summary>Retention policy inside a subject export.</summary>
public sealed record SubjectExportPolicy
{
    /// <summary>Data category.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Retention period in days.</summary>
    public int RetentionDays { get; init; }

    /// <summary>Recorded legal basis.</summary>
    public string? LegalBasis { get; init; }
}
