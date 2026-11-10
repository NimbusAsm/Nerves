using Microsoft.Extensions.Options;
using Nerves.Core;
using Nerves.Notification.Options;

namespace Nerves.Notification.Delivery;

/// <summary>
/// Sweeps pending notifications every <c>Delivery:SweepSeconds</c> and hands them
/// to <see cref="NotificationDispatcher"/>. Failures of one sweep never stop the
/// worker; the next tick retries.
/// </summary>
public sealed class NotificationWorker(
    IServiceScopeFactory scopes,
    IOptions<DeliveryOptions> options,
    IClock clock,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> Swept =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1, "NotificationSweep"), "processed {Count} notifications");

    private static readonly Action<ILogger, Exception?> SweepFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(2, "NotificationSweepFailed"), "notification sweep failed");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var seconds = Math.Clamp(settings.SweepSeconds, 5, 3600);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
                var processed = await dispatcher.DispatchDueAsync(
                    clock.UtcNow,
                    settings.BatchSize,
                    stoppingToken);

                if (processed > 0)
                {
                    Swept(logger, processed, null);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                SweepFailed(logger, exception);
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
}
