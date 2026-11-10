using Microsoft.EntityFrameworkCore;
using Nerves.Notification.Domain;

namespace Nerves.Notification.Persistence;

/// <summary>EF Core implementation of <see cref="INotificationStore"/>.</summary>
public sealed class EfNotificationStore(NotificationDbContext db) : INotificationStore
{
    public async Task<EventCursor?> GetEventCursorAsync(string consumer, CancellationToken cancellationToken)
    {
        var state = await db.EventConsumerStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Consumer == consumer, cancellationToken);

        return state is null ? null : new EventCursor(state.LastOccurredAt, state.LastEventId);
    }

    public Task<bool> HasMessageForEventAsync(Guid sourceEventId, CancellationToken cancellationToken) =>
        db.Messages.AsNoTracking().AnyAsync(x => x.SourceEventId == sourceEventId, cancellationToken);

    public void AddMessage(NotificationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        db.Messages.Add(message);
    }

    public void SetEventCursor(string consumer, EventCursor cursor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        var state = db.EventConsumerStates.Find(consumer);
        if (state is null)
        {
            db.EventConsumerStates.Add(EventConsumerState.Create(consumer, cursor.LastOccurredAt, cursor.LastEventId, now));
            return;
        }

        state.Advance(cursor.LastOccurredAt, cursor.LastEventId, now);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
