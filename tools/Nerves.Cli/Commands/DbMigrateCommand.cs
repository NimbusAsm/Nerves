using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Nerves.Cli.CommandLine;
using Nerves.Cli.Infrastructure;

namespace Nerves.Cli.Commands;

/// <summary>
/// Applies the EF Core migrations each service ships. This is the same code path
/// Kubernetes Jobs use (`Database:MigrateOnly=true`), just driven locally, and it
/// continues past a failing schema so one broken database does not hide the rest.
/// </summary>
public static class DbMigrateCommand
{
    public static async Task<int> RunAsync(
        CliArguments args,
        CliOutput output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        var settings = CliSettings.From(args);

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            output.Line("error: no connection string; pass --connection-string or set ConnectionStrings__Default");
            return ExitCodes.Usage;
        }

        var targets = DatabaseTargets.Select(args.ListValue("service"));
        if (targets.Count == 0)
        {
            output.Line("error: unknown --service value; expected any of " + string.Join(", ", DatabaseTargets.All.Select(t => t.Name)));
            return ExitCodes.Usage;
        }

        var dryRun = args.Has("dry-run");
        var reports = new List<MigrationReport>();
        var rows = new List<IReadOnlyList<string>>();
        var failed = 0;

        foreach (var target in targets)
        {
            var started = Stopwatch.GetTimestamp();

            try
            {
                await using var database = target.CreateContext(settings.ConnectionString);

                var pending = (await database.Database
                    .GetPendingMigrationsAsync(cancellationToken)
                    .ConfigureAwait(false))
                    .ToArray();

                if (!dryRun && pending.Length > 0)
                {
                    await database.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                }

                var elapsed = Elapsed(started);
                reports.Add(new MigrationReport(target.Name, target.Schema, pending.Length, !dryRun, true, null, elapsed));
                rows.Add([
                    target.Name,
                    target.Schema,
                    pending.Length.ToString(CultureInfo.InvariantCulture),
                    pending.Length == 0 ? "up to date" : dryRun ? "would apply" : "applied",
                    elapsed.ToString(CultureInfo.InvariantCulture) + " ms",
                ]);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
                var elapsed = Elapsed(started);
                reports.Add(new MigrationReport(target.Name, target.Schema, 0, false, false, exception.Message, elapsed));
                rows.Add([target.Name, target.Schema, "-", "failed", exception.Message]);
            }
        }

        if (output.Json)
        {
            output.Emit(new
            {
                generatedAt = CliOutput.Timestamp(),
                dryRun,
                databases = reports,
            });

            return failed == 0 ? ExitCodes.Success : ExitCodes.Failure;
        }

        output.Table(["service", "schema", "pending", "result", "elapsed"], rows);
        output.Line(Summarize(reports, dryRun, failed));

        return failed == 0 ? ExitCodes.Success : ExitCodes.Failure;
    }

    private static string Summarize(IReadOnlyList<MigrationReport> reports, bool dryRun, int failed)
    {
        if (failed > 0)
        {
            return failed.ToString(CultureInfo.InvariantCulture) + " database(s) failed";
        }

        var pending = reports.Sum(report => report.Pending);

        if (!dryRun)
        {
            return "all databases are up to date";
        }

        return pending == 0
            ? "dry run: all databases are up to date"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"dry run: {pending} migration(s) would be applied");
    }

    private static long Elapsed(long started) =>
        (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
