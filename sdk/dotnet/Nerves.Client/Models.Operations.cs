namespace Nerves.Client;

/// <summary>One active or revoked login session.</summary>
public sealed record SessionInfo
{
    /// <summary>Server-assigned session id.</summary>
    public Guid Id { get; init; }

    /// <summary>Device identifier chosen by the client.</summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last time the session was used.</summary>
    public DateTimeOffset LastSeenAt { get; init; }

    /// <summary>Hard expiry that cannot be extended.</summary>
    public DateTimeOffset AbsoluteExpiresAt { get; init; }

    /// <summary>Whether the session was revoked.</summary>
    public bool Revoked { get; init; }

    /// <summary>Why the session was revoked, when it was.</summary>
    public string? RevokedReason { get; init; }
}

/// <summary>One permission code.</summary>
public sealed record PermissionInfo
{
    /// <summary>Server-assigned identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Unique permission code, e.g. <c>accounts.read</c>.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable description.</summary>
    public string? Description { get; init; }
}

/// <summary>A group of accounts.</summary>
public sealed record Group
{
    /// <summary>Server-assigned identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Realm the group lives in.</summary>
    public string Realm { get; init; } = string.Empty;

    /// <summary>Unique code within the realm.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>A group plus its member count.</summary>
public sealed record GroupDetail
{
    /// <summary>Server-assigned identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Realm the group lives in.</summary>
    public string Realm { get; init; } = string.Empty;

    /// <summary>Unique code within the realm.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Number of member accounts.</summary>
    public int MemberCount { get; init; }
}

/// <summary>One group membership.</summary>
public sealed record GroupMember
{
    /// <summary>Member account id.</summary>
    public Guid AccountId { get; init; }

    /// <summary>When the account joined the group.</summary>
    public DateTimeOffset AddedAt { get; init; }
}

/// <summary>Result of a single authorization decision.</summary>
public sealed record DecisionResult
{
    /// <summary>Whether the permission is granted.</summary>
    public bool Allowed { get; init; }

    /// <summary>Why the decision was reached, when the PDP supplied a reason.</summary>
    public string? Reason { get; init; }

    /// <summary>Where the decision came from: <c>local</c> or <c>pdp</c>.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Version of the subject's authorization data.</summary>
    public long AuthorizationVersion { get; init; }

    /// <summary>The permission that was checked.</summary>
    public string Permission { get; init; } = string.Empty;
}

/// <summary>Permission cache statistics.</summary>
public sealed record DecisionStats
{
    /// <summary>Cached subject entries.</summary>
    public int Entries { get; init; }

    /// <summary>Cache hits since start.</summary>
    public long Hits { get; init; }

    /// <summary>Cache misses since start.</summary>
    public long Misses { get; init; }

    /// <summary>Hit ratio in the range 0..1.</summary>
    public double HitRate { get; init; }
}

/// <summary>One append-only audit record.</summary>
public sealed record AuditEvent
{
    /// <summary>Server-assigned identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Hash-chained stream the event belongs to.</summary>
    public string Stream { get; init; } = string.Empty;

    /// <summary>Monotonic sequence within the stream.</summary>
    public long Sequence { get; init; }

    /// <summary>When the event happened.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Actor kind, e.g. <c>admin</c> or <c>account</c>.</summary>
    public string ActorType { get; init; } = string.Empty;

    /// <summary>Actor identifier.</summary>
    public string? ActorId { get; init; }

    /// <summary>Dotted action name.</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Target kind.</summary>
    public string? TargetType { get; init; }

    /// <summary>Target identifier.</summary>
    public string? TargetId { get; init; }

    /// <summary>Outcome, e.g. <c>success</c> or <c>failure</c>.</summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>Correlation id of the originating request.</summary>
    public string? TraceId { get; init; }

    /// <summary>Chain hash of this record.</summary>
    public string Hash { get; init; } = string.Empty;
}

/// <summary>Result of verifying one audit stream.</summary>
public sealed record AuditVerifyResult
{
    /// <summary>Whether the whole chain is intact.</summary>
    public bool Valid { get; init; }

    /// <summary>First broken event, when the chain is broken.</summary>
    public Guid? BrokenAt { get; init; }

    /// <summary>Machine-readable failure reason.</summary>
    public string? Reason { get; init; }

    /// <summary>Number of verified events.</summary>
    public long Count { get; init; }
}

/// <summary>A downloaded binary document (audit export).</summary>
public sealed record NervesFile
{
    /// <summary>Raw response bytes.</summary>
    public ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>Media type reported by the server.</summary>
    public string ContentType { get; init; } = "application/octet-stream";

    /// <summary>File name from <c>Content-Disposition</c>, when present.</summary>
    public string? FileName { get; init; }
}

/// <summary>One consent record.</summary>
public sealed record ConsentRecord
{
    /// <summary>Server-assigned identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Data subject identifier.</summary>
    public string SubjectId { get; init; } = string.Empty;

    /// <summary>Processing purpose.</summary>
    public string Purpose { get; init; } = string.Empty;

    /// <summary>Version of the consent text that was accepted.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>When consent was granted.</summary>
    public DateTimeOffset GrantedAt { get; init; }

    /// <summary>When consent expires.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>When consent was revoked.</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>Why consent was revoked.</summary>
    public string? RevocationReason { get; init; }
}

/// <summary>A retention policy for one data category.</summary>
public sealed record RetentionPolicy
{
    /// <summary>Server-assigned identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Data category, e.g. <c>audit</c>.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Retention period in days.</summary>
    public int RetentionDays { get; init; }

    /// <summary>Legal basis recorded for the policy.</summary>
    public string? LegalBasis { get; init; }

    /// <summary>Last modification timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Aggregate counts for the compliance module.</summary>
public sealed record ComplianceSummary
{
    /// <summary>Total consent records.</summary>
    public int Consents { get; init; }

    /// <summary>Consents that are granted and unexpired.</summary>
    public int Active { get; init; }

    /// <summary>Consents that were revoked.</summary>
    public int Revoked { get; init; }

    /// <summary>Number of retention policies.</summary>
    public int RetentionPolicies { get; init; }
}

/// <summary>Result of an erasure request.</summary>
public sealed record EraseResult
{
    /// <summary>Erasure request id.</summary>
    public Guid RequestId { get; init; }

    /// <summary>Data subject identifier.</summary>
    public string SubjectId { get; init; } = string.Empty;

    /// <summary><c>pending</c>, <c>completed</c> or <c>failed</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Number of consents revoked by the erasure.</summary>
    public int ConsentRevoked { get; init; }

    /// <summary>Whether the backing account was disabled.</summary>
    public bool AccountDisabled { get; init; }
}

/// <summary>One recorded erasure request.</summary>
public sealed record ErasureRecord
{
    /// <summary>Erasure request id.</summary>
    public Guid Id { get; init; }

    /// <summary><c>pending</c>, <c>completed</c> or <c>failed</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Reason supplied by the operator.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>When erasure was requested.</summary>
    public DateTimeOffset RequestedAt { get; init; }

    /// <summary>When erasure finished, if it did.</summary>
    public DateTimeOffset? CompletedAt { get; init; }
}

/// <summary>One queued or delivered notification.</summary>
public sealed record NotificationMessage
{
    /// <summary>Server-assigned identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Recipient address.</summary>
    public string To { get; init; } = string.Empty;

    /// <summary>Template code that produced the body.</summary>
    public string Template { get; init; } = string.Empty;

    /// <summary>Rendered subject.</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary><c>pending</c>, <c>sent</c> or <c>failed</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Delivery attempts so far.</summary>
    public int Attempts { get; init; }

    /// <summary>Last delivery error, if any.</summary>
    public string? Error { get; init; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Delivery timestamp, if delivered.</summary>
    public DateTimeOffset? SentAt { get; init; }

    /// <summary>Next retry timestamp, if a retry is scheduled.</summary>
    public DateTimeOffset? NextAttemptAt { get; init; }
}

/// <summary>One identity domain event.</summary>
public sealed record IdentityEvent
{
    /// <summary>Event id.</summary>
    public Guid Id { get; init; }

    /// <summary>Dotted event type, e.g. <c>account.created</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Related account, when the event is about one.</summary>
    public Guid? AccountId { get; init; }

    /// <summary>Related legal entity, when the event is about one.</summary>
    public Guid? LegalEntityId { get; init; }

    /// <summary>Raw JSON payload.</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>When the event happened.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>When the event was dispatched to consumers.</summary>
    public DateTimeOffset? DispatchedAt { get; init; }
}
