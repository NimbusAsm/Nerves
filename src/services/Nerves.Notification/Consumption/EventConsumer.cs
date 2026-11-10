using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nerves.Core;
using Nerves.Notification.Domain;
using Nerves.Notification.Options;
using Nerves.Notification.Persistence;
using Nerves.Notification.Templates;

namespace Nerves.Notification.Consumption;

/// <summary>
/// Turns identity events into queued notifications. Idempotent twice over: the
/// stored cursor limits how much of the stream is read, and <c>SourceEventId</c>
/// (unique in the database) makes a replayed event a no-op.
/// </summary>
public sealed class EventConsumer(
    IIdentityEventClient events,
    INotificationStore store,
    IOptions<EventConsumerOptions> options,
    IClock clock,
    ILogger<EventConsumer> logger)
{
    /// <summary>Name the cursor is persisted under.</summary>
    public const string ConsumerName = "identity-events";

    private static readonly Action<ILogger, Guid, string, string, Exception?> EventSkipped =
        LoggerMessage.Define<Guid, string, string>(
            LogLevel.Warning,
            new EventId(1, "IdentityEventSkipped"),
            "skipped identity event: id={EventId} type={EventType} reason={Reason}");

    private static readonly Action<ILogger, int, int, Exception?> BatchConsumed =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(2, "IdentityEventsConsumed"),
            "consumed {Created} notifications from {Read} identity events");

    /// <summary>
    /// Reads the next batch and queues one message per mapped event. Returns how
    /// many notifications were queued.
    /// </summary>
    public async Task<int> ConsumeOnceAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var cursor = await store.GetEventCursorAsync(ConsumerName, cancellationToken);

        // Re-read a small overlap: the stream is ordered by occurrence time only,
        // so events sharing a timestamp would otherwise be skipped.
        var after = cursor is null
            ? (DateTimeOffset?)null
            : cursor.LastOccurredAt.AddSeconds(-settings.LookbackSeconds);

        var batch = await events.ListEventsAsync(after, settings.BatchSize, cancellationToken);
        if (batch.Count == 0)
        {
            return 0;
        }

        var now = clock.UtcNow;
        var created = 0;
        EventCursor? next = null;

        foreach (var envelope in batch.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id))
        {
            next = new EventCursor(envelope.OccurredAt, envelope.Id);

            if (await store.HasMessageForEventAsync(envelope.Id, cancellationToken))
            {
                continue;
            }

            var account = envelope.AccountId is { } accountId
                ? await events.GetAccountAsync(accountId, cancellationToken)
                : null;

            var mapped = EventTemplateMapper.Map(envelope, account, now, settings.Issuer);
            if (mapped.IsFailure)
            {
                EventSkipped(logger, envelope.Id, envelope.Type, mapped.Reason.Code, null);
                continue;
            }

            var rendered = EmailTemplateCatalog.Render(mapped.Value.Template, mapped.Value.Values);
            if (rendered.IsFailure)
            {
                EventSkipped(logger, envelope.Id, envelope.Type, rendered.Reason.Code, null);
                continue;
            }

            store.AddMessage(NotificationMessage.Create(
                mapped.Value.Channel,
                mapped.Value.Recipient,
                rendered.Value.Template,
                rendered.Value.Subject,
                rendered.Value.Body,
                now,
                envelope.Id));

            created += 1;
        }

        if (next is not null)
        {
            store.SetEventCursor(ConsumerName, next, now);
            await store.SaveChangesAsync(cancellationToken);
        }

        BatchConsumed(logger, created, batch.Count, null);
        return created;
    }
}
