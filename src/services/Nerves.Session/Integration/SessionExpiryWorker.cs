using Microsoft.Extensions.Options;
using Nerves.Session.Options;
using Nerves.Session.Persistence;

namespace Nerves.Session.Integration;

/// <summary>
/// Marks sessions that passed their idle or absolute deadline as revoked, so
/// listings and downstream checks never treat them as active.
/// </summary>
public sealed class SessionExpiryWorker(
    IServiceScopeFactory scopes,
    IOptions<SessionLifetimeOptions> options,
    ILogger<SessionExpiryWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> Swept =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1, "SessionSweep"), "Revoked {Count} expired sessions");

    private static readonly Action<ILogger, Exception?> SweepFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(2, "SessionSweepFailed"), "Session expiry sweep failed");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = Math.Clamp(options.Value.ExpirySweepSeconds, 5, 3600);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
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

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<SessionStore>();

        var revoked = await store.SweepAsync(DateTimeOffset.UtcNow, cancellationToken);
        if (revoked > 0)
        {
            Swept(logger, revoked, null);
        }
    }
}
