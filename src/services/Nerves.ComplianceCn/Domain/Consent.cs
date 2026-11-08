namespace Nerves.ComplianceCn.Domain;

/// <summary>What a consent covers, for example the privacy policy of one product.</summary>
public sealed class Consent
{
    private Consent() => Purpose = SubjectId = Version = string.Empty;

    private Consent(
        Guid id,
        string subjectId,
        string purpose,
        string version,
        DateTimeOffset grantedAt,
        DateTimeOffset? expiresAt)
    {
        Id = id;
        SubjectId = subjectId;
        Purpose = purpose;
        Version = version;
        GrantedAt = grantedAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public string SubjectId { get; private set; }

    public string Purpose { get; private set; }

    public string Version { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevocationReason { get; private set; }

    public bool IsActive => RevokedAt is null && (ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow);

    public static Consent Grant(
        string subjectId,
        string purpose,
        string version,
        DateTimeOffset grantedAt,
        DateTimeOffset? expiresAt) => new(Guid.CreateVersion7(grantedAt), subjectId, purpose, version, grantedAt, expiresAt);

    public void Revoke(DateTimeOffset now, string? reason)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevocationReason = reason;
    }
}
