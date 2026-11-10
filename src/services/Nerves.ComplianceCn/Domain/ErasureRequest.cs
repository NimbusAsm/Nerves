namespace Nerves.ComplianceCn.Domain;

/// <summary>
/// A data subject erasure request. The record is the audit anchor: it survives
/// the deletion of the subject's data so the erasure itself can be proven.
/// </summary>
public sealed class ErasureRequest
{
    private ErasureRequest() => SubjectId = Reason = Status = string.Empty;

    private ErasureRequest(Guid id, string subjectId, string reason, DateTimeOffset requestedAt)
    {
        Id = id;
        SubjectId = subjectId;
        Reason = reason;
        RequestedAt = requestedAt;
        Status = "pending";
    }

    public Guid Id { get; private set; }

    public string SubjectId { get; private set; }

    public string Reason { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string Status { get; private set; }

    public static ErasureRequest Create(string subjectId, string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        return new ErasureRequest(Guid.CreateVersion7(now), subjectId, reason, now);
    }

    public void Complete(DateTimeOffset now)
    {
        Status = "completed";
        CompletedAt = now;
    }

    public void Fail(DateTimeOffset now)
    {
        Status = "failed";
        CompletedAt = now;
    }

    /// <summary>
    /// Replaces the subject identifier with a constant marker. CompletedAt is
    /// reset so the record survives one more full retention window before the
    /// sweep deletes it.
    /// </summary>
    public void Anonymize(DateTimeOffset now)
    {
        SubjectId = AnonymizedSubject;
        Reason = string.Empty;
        Status = "anonymized";
        CompletedAt = now;
    }

    public const string AnonymizedSubject = "anonymized";
}
