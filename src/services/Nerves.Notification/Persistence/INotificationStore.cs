using Nerves.Notification.Domain;

namespace Nerves.Notification.Persistence;

/// <summary>Position in the identity event stream.</summary>
public sealed record EventCursor(DateTimeOffset LastOccurredAt, Guid? LastEventId);

/// <summary>
/// Write side used by the event consumer. Keeping it behind an interface lets the
/// consumer's idempotency and skip rules be unit tested without a database.
/// </summary>
public interface INotificationStore
{
    Task<EventCursor?> GetEventCursorAsync(string consumer, CancellationToken cancellationToken);

    /// <summary>Whether an event was already turned into a message.</summary>
    Task<bool> HasMessageForEventAsync(Guid sourceEventId, CancellationToken cancellationToken);

    void AddMessage(NotificationMessage message);

    void SetEventCursor(string consumer, EventCursor cursor, DateTimeOffset now);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
