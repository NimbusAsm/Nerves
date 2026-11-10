namespace Nerves.Notification.Persistence;

/// <summary>
/// Cursor of one event consumer. Stored in the database so a restart resumes
/// where the previous process stopped instead of replaying the whole stream.
/// </summary>
public sealed class EventConsumerState
{
    private EventConsumerState()
    {
        Consumer = string.Empty;
    }

    private EventConsumerState(string consumer, DateTimeOffset occurredAt, Guid? eventId, DateTimeOffset now)
    {
        Consumer = consumer;
        LastOccurredAt = occurredAt;
        LastEventId = eventId;
        UpdatedAt = now;
    }

    /// <summary>Stable name of the consumer, for example <c>identity-events</c>.</summary>
    public string Consumer { get; private set; }

    public DateTimeOffset LastOccurredAt { get; private set; }

    public Guid? LastEventId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static EventConsumerState Create(string consumer, DateTimeOffset occurredAt, Guid? eventId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);

        return new EventConsumerState(consumer, occurredAt, eventId, now);
    }

    /// <summary>Moves the cursor forward; never moves it backwards.</summary>
    public void Advance(DateTimeOffset occurredAt, Guid? eventId, DateTimeOffset now)
    {
        if (occurredAt <= LastOccurredAt)
        {
            return;
        }

        LastOccurredAt = occurredAt;
        LastEventId = eventId;
        UpdatedAt = now;
    }
}
