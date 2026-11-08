using Microsoft.Extensions.Options;

namespace Nerves.Audit.Archiving;

/// <summary>Periodic archive pass; disabled when the interval is zero.</summary>
public sealed class AuditArchiveWorker(
    IServiceProvider services,
    IOptions<ArchiveOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.IntervalSeconds;
        if (interval <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var archive = scope.ServiceProvider.GetRequiredService<AuditArchiveService>();
                await archive.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // A failed pass is retried on the next tick; events stay pending.
            }
        }
    }
}
