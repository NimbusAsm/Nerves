using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nerves.Notification.Options;

namespace Nerves.Notification.Consumption;

/// <summary>
/// Polls the identity event stream every <c>Events:Consumer:PollSeconds</c>. A
/// failed poll leaves the cursor untouched, so the next tick retries the same
/// batch; one bad event never stops the loop.
/// </summary>
public sealed class EventConsumerWorker(
    IServiceScopeFactory scopes,
    IOptions<EventConsumerOptions> options,
    ILogger<EventConsumerWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> PollFailed =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(1, "IdentityEventPollFailed"),
            "identity event poll failed; retrying in {Seconds} seconds");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var seconds = Math.Clamp(settings.PollSeconds, 5, 3600);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var consumer = scope.ServiceProvider.GetRequiredService<EventConsumer>();
                await consumer.ConsumeOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                PollFailed(logger, seconds, exception);
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
