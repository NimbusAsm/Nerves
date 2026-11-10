namespace Nerves.Session.Domain;

/// <summary>
/// A domain event stored in the same transaction as the state change it
/// describes, then delivered to the audit service asynchronously. Keeps the
/// audit trail at-least-once even when the audit service is unavailable.
/// </summary>
public sealed class OutboxEvent
{
    private OutboxEvent() => Type = Payload = string.Empty;

    private OutboxEvent(Guid id, string type, string? targetType, string? targetId, string payload, DateTimeOffset now)
    {
        Id = id;
        Type = type;
        TargetType = targetType;
        TargetId = targetId;
        Payload = payload;
        OccurredAt = now;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; }

    public string? TargetType { get; private set; }

    public string? TargetId { get; private set; }

    public string Payload { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? DispatchedAt { get; private set; }

    public int Attempts { get; private set; }

    public static OutboxEvent Create(
        string type,
        string? targetType,
        string? targetId,
        string payload,
        DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), type, targetType, targetId, payload, now);

    public void MarkDispatched(DateTimeOffset now)
    {
        DispatchedAt = now;
        Attempts++;
    }

    public void MarkAttempt()
    {
        Attempts++;
    }
}
