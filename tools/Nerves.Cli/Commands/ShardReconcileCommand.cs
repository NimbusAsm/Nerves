using System.Globalization;
using Microsoft.Extensions.Options;
using Nerves.Cli.CommandLine;
using Nerves.Cli.Infrastructure;
using Nerves.Identity.Persistence;
using Nerves.Identity.Sharding;

namespace Nerves.Cli.Commands;

/// <summary>
/// Recomputes <c>identity.routing_index.shard</c>. The shard is a pure function
/// of the account id, so this is required whenever `Sharding:ShardCount` changes;
/// the identity service performs the same pass on startup when
/// `Sharding:Recompute` is set.
/// </summary>
public static class ShardReconcileCommand
{
    public static async Task<int> RunAsync(
        CliArguments args,
        CliOutput output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        if (!int.TryParse(
                args.Value("shard-count"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var shardCount) || shardCount < 1)
        {
            output.Line("error: --shard-count <n> is required and must be at least 1");
            return ExitCodes.Usage;
        }

        var settings = CliSettings.From(args);
        var dryRun = args.Has("dry-run");
        var started = DateTimeOffset.UtcNow;

        await using var database = new IdentityDbContext(
            DatabaseTargets.Options<IdentityDbContext>(settings.ConnectionString));

        var resolver = new ShardResolver(Options.Create(new ShardingOptions { ShardCount = shardCount }));
        var reconciler = new RoutingIndexReconciler(database, resolver);
        var report = await reconciler.RunAsync(dryRun, cancellationToken).ConfigureAwait(false);

        if (output.Json)
        {
            output.Emit(new
            {
                generatedAt = CliOutput.Timestamp(),
                shardCount,
                dryRun,
                report.Scanned,
                report.Changed,
                distribution = report.Distribution.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value),
                elapsedMilliseconds = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds,
            });

            return ExitCodes.Success;
        }

        output.Table(
            ["shard", "accounts"],
            report.Distribution
                .OrderBy(pair => pair.Key)
                .Select(pair => (IReadOnlyList<string>)[
                    pair.Key.ToString(CultureInfo.InvariantCulture),
                    pair.Value.ToString(CultureInfo.InvariantCulture),
                ])
                .ToArray());

        output.Line(string.Create(
            CultureInfo.InvariantCulture,
            $"scanned={report.Scanned} changed={report.Changed} dryRun={dryRun} shards={shardCount}"));

        return ExitCodes.Success;
    }
}
