using System.Text.Json;
using Microsoft.Extensions.Options;
using Nerves.ComplianceCn.Domain;
using Nerves.ComplianceCn.Options;
using Nerves.ComplianceCn.Persistence;

namespace Nerves.ComplianceCn.Integration;

/// <summary>
/// Applies the stored retention policies on a schedule. Writes one outbox row
/// per run so the audit trail is durable even when the sweep deletes data.
/// </summary>
public sealed class RetentionWorker(
    IServiceScopeFactory scopes,
    IOptions<RetentionOptions> options,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, int, int, Exception?> Swept =
        LoggerMessage.Define<int, int, int>(
            LogLevel.Information,
            new EventId(1, "RetentionSweep"),
            "Retention sweep removed {Consents} consents, anonymized {Anonymized} erasures, removed {Erasures} erasures");

    private static readonly Action<ILogger, Exception?> SweepFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(2, "RetentionSweepFailed"), "Retention sweep failed; will retry next interval");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(settings.StartDelaySeconds, 0, 600)), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.ClampInterval()));

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

    /// <summary>Runs one sweep; exposed so operators and tests can force it.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
            var sweeper = scope.ServiceProvider.GetRequiredService<RetentionSweeper>();
            var now = DateTimeOffset.UtcNow;

            var result = await sweeper.SweepAsync(settings, now, cancellationToken);

            // Nothing to delete is the common case: skip the audit row so the
            // stream stays signal, not noise. The sweep still logs locally.
            if (result.Skipped || !result.HasChanges)
            {
                return;
            }

            var payload = JsonSerializer.Serialize(new
            {
                policiesApplied = result.PoliciesApplied,
                consentsRemoved = result.ConsentsRemoved,
                erasuresAnonymized = result.ErasuresAnonymized,
                erasuresRemoved = result.ErasuresRemoved,
            });

            // The outbox row joins the same transaction as the deletions, so a
            // crash cannot delete data without leaving the audit intent behind.
            db.OutboxEvents.Add(OutboxEvent.Create(
                "compliance.retention.swept",
                "compliance",
                "retention",
                payload,
                now));

            await db.SaveChangesAsync(cancellationToken);

            Swept(logger, result.ConsentsRemoved, result.ErasuresAnonymized, result.ErasuresRemoved, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SweepFailed(logger, exception);
        }
    }
}
