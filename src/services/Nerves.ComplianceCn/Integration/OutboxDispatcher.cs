using Microsoft.EntityFrameworkCore;
using Nerves.ComplianceCn.Persistence;

namespace Nerves.ComplianceCn.Integration;

/// <summary>Delivers pending compliance outbox events to the audit stream.</summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopes,
    IComplianceAuditPublisher publisher,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 100;

    private static readonly Action<ILogger, Exception?> DispatchFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1, "OutboxDispatch"), "Compliance outbox dispatch failed; will retry");

    private static readonly Action<ILogger, Guid, Exception?> EventFailed =
        LoggerMessage.Define<Guid>(LogLevel.Warning, new EventId(2, "OutboxEvent"), "Failed to dispatch compliance outbox event {EventId}");

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
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();

        var pending = await db.OutboxEvents
            .Where(x => x.DispatchedAt == null && x.Attempts < 10)
            .OrderBy(x => x.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var item in pending)
        {
            try
            {
                await publisher.AppendAsync(
                    item.Type,
                    "system",
                    null,
                    item.TargetType,
                    item.TargetId,
                    "success",
                    item.Payload,
                    cancellationToken);

                item.MarkDispatched(DateTimeOffset.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                item.MarkAttempt();
                EventFailed(logger, item.Id, exception);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
