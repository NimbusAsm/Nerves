using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nerves.Notification.Domain;
using Nerves.Notification.Options;
using Nerves.Notification.Persistence;

namespace Nerves.Notification.Delivery;

/// <summary>
/// Claims due notifications and runs one delivery attempt for each. Shared by
/// the background worker and by tests that drive the loop directly.
/// </summary>
public sealed class NotificationDispatcher(
    NotificationDbContext db,
    DeliveryAttemptRunner runner,
    IOptions<DeliveryOptions> options)
{
    /// <summary>
    /// Claims up to <paramref name="batchSize"/> due messages and delivers them.
    /// Returns how many messages were processed (sent or failed).
    /// </summary>
    public async Task<int> DispatchDueAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        var policy = options.Value.ToRetryPolicy();
        var due = await db.Messages
            .Where(x => x.Status == NotificationStatus.Pending && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .ThenBy(x => x.CreatedAt)
            .Take(Math.Clamp(batchSize, 1, 200))
            .ToListAsync(cancellationToken);

        return await ProcessAsync(due, now, policy, cancellationToken);
    }

    /// <summary>Runs the attempts for an already-claimed batch and saves the result.</summary>
    public async Task<int> ProcessAsync(
        IReadOnlyCollection<NotificationMessage> messages,
        DateTimeOffset now,
        RetryPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(policy);

        if (messages.Count == 0)
        {
            return 0;
        }

        foreach (var message in messages)
        {
            if (!message.IsDue(now))
            {
                continue;
            }

            await runner.DeliverAsync(message, policy, now, cancellationToken);
        }

        // One save for the whole batch: the worker is the only writer of status
        // transitions, and a crash before SaveChanges simply retries the batch.
        await db.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }
}
