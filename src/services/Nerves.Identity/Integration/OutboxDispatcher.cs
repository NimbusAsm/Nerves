using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Persistence;

namespace Nerves.Identity.Integration;

/// <summary>Delivers pending outbox events to the audit stream.</summary>
/// <remarks>
/// Outbox rows are written to the shard that owns the account, so a full drain
/// walks the global database and every account shard.
/// </remarks>
public sealed class OutboxDispatcher(
    IdentityStore store,
    IAuditPublisher publisher,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 100;

    private static readonly Action<ILogger, Exception?> DispatchFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1, "OutboxDispatch"), "Outbox dispatch failed; will retry");

    private static readonly Action<ILogger, Guid, Exception?> EventFailed =
        LoggerMessage.Define<Guid>(LogLevel.Warning, new EventId(2, "OutboxEvent"), "Failed to dispatch outbox event {EventId}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                DispatchFailed(logger, exception);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        // Global first: legal-entity scoped events live there.
        await using (var global = store.Global())
        {
            await DrainAsync(global, cancellationToken);
        }

        for (var index = 0; index < store.ShardCount; index++)
        {
            await using var shard = store.Plane.CreateShard(index);
            await DrainAsync(shard, cancellationToken);
        }
    }

    private async Task DrainAsync(IdentityDbContext db, CancellationToken cancellationToken)
    {
        var pending = await db.OutboxEvents
            .Where(x => x.DispatchedAt == null && x.Attempts < 10)
            .OrderBy(x => x.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var item in pending)
        {
            try
            {
                await publisher.AppendAsync(
                    item.Type,
                    "system",
                    null,
                    item.AccountId is null ? "legal-entity" : "account",
                    (item.AccountId ?? item.LegalEntityId)?.ToString(),
                    "success",
                    null,
                    item.Payload,
                    cancellationToken);

                item.MarkDispatched(DateTimeOffset.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                item.MarkAttempt(DateTimeOffset.UtcNow);
                EventFailed(logger, item.Id, exception);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
