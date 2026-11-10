namespace Nerves.Identity.Domain;

/// <summary>
/// A domain event stored in the same transaction as the state change it
/// describes, then delivered asynchronously. Guarantees at-least-once delivery
/// even when the audit service is briefly unavailable.
/// </summary>
public sealed class OutboxEvent
{
    private OutboxEvent() => Type = Payload = string.Empty;

    private OutboxEvent(Guid id, string type, Guid? accountId, Guid? legalEntityId, string payload, DateTimeOffset now)
    {
        Id = id;
        Type = type;
        AccountId = accountId;
        LegalEntityId = legalEntityId;
        Payload = payload;
        OccurredAt = now;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; }

    public Guid? AccountId { get; private set; }

    public Guid? LegalEntityId { get; private set; }

    public string Payload { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? DispatchedAt { get; private set; }

    public int Attempts { get; private set; }

    public static OutboxEvent Create(
        string type,
        Guid? accountId,
        Guid? legalEntityId,
        string payload,
        DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), type, accountId, legalEntityId, payload, now);

    public void MarkDispatched(DateTimeOffset now)
    {
        DispatchedAt = now;
        Attempts++;
    }

    public void MarkAttempt(DateTimeOffset now)
    {
        Attempts++;
        _ = now;
    }
}
