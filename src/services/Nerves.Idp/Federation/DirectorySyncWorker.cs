using Microsoft.Extensions.Options;
using Nerves.Idp.Options;

namespace Nerves.Idp.Federation;

/// <summary>
/// Periodically runs <see cref="IDirectorySyncRunner"/>. The worker is a
/// reconciler, not a mirror: it only creates and updates, never deletes.
/// </summary>
public sealed class DirectorySyncWorker(
    IServiceScopeFactory scopes,
    IOptions<DirectorySyncOptions> syncOptions,
    DirectorySyncState state,
    ILogger<DirectorySyncWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, int, int, int, Exception?> RunCompleted =
        LoggerMessage.Define<int, int, int, int>(
            LogLevel.Information,
            new EventId(1, "DirectorySync"),
            "Directory sync finished: created={Created} updated={Updated} unchanged={Unchanged} skipped={Skipped}");

    private static readonly Action<ILogger, Exception?> RunFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(2, "DirectorySyncFailed"), "Directory sync failed; will retry next interval");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = syncOptions.Value;
        if (!options.Enabled)
        {
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Clamp(options.StartDelaySeconds, 0, 600));
        try
        {
            await Task.Delay(delay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Clamp(options.IntervalMinutes, 1, 1440));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

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

    /// <summary>Runs one pass; exposed so operators and tests can force a sync.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        state.Started(startedAt);

        DirectorySyncSnapshot snapshot;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var runner = scope.ServiceProvider.GetRequiredService<IDirectorySyncRunner>();
            snapshot = await runner.RunOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            RunFailed(logger, exception);
            snapshot = DirectorySyncSnapshot.FailedRun(startedAt, exception.GetType().Name);
        }

        state.Completed(snapshot);

        if (snapshot.LastError is null)
        {
            RunCompleted(logger, snapshot.Created, snapshot.Updated, snapshot.Unchanged, snapshot.Skipped, null);
        }
        else
        {
            RunFailed(logger, null);
        }
    }
}
