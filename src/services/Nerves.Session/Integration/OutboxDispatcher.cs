using Nerves.Session.Persistence;

namespace Nerves.Session.Integration;

/// <summary>Delivers pending outbox events to the audit stream.</summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopes,
    IAuditPublisher publisher,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

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
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<SessionStore>();

        _ = await store.DispatchOutboxAsync(publisher, cancellationToken);
    }
}
