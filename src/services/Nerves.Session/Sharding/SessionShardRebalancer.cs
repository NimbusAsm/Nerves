using Microsoft.EntityFrameworkCore;
using Nerves.Session.Persistence;

namespace Nerves.Session.Sharding;

/// <summary>Result of a shard rebalance pass.</summary>
public sealed record SessionRebalanceReport(int Scanned, int Moved, bool DryRun);

/// <summary>
/// Moves sessions from a previous shard topology to the new one. Reads the old
/// databases from <c>Sharding:PreviousConnectionStrings:0..M</c> and writes to
/// the configured <see cref="SessionStore"/> shards. Idempotent: rows already in
/// the target shard are left alone.
/// </summary>
public sealed class SessionShardRebalancer(
    IConfiguration configuration,
    SessionStore store,
    ILogger<SessionShardRebalancer> logger)
{
    private static readonly Action<ILogger, int, int, bool, Exception?> Reported =
        LoggerMessage.Define<int, int, bool>(
            LogLevel.Information,
            new EventId(1, "SessionRebalance"),
            "session shard rebalance: scanned={Scanned} moved={Moved} dryRun={DryRun}");

    public async Task<SessionRebalanceReport> RunAsync(bool dryRun, CancellationToken cancellationToken)
    {
        var previous = PreviousShards(configuration);
        var scanned = 0;
        var moved = 0;

        foreach (var source in previous)
        {
            await using var sourceDb = new SessionDbContext(source);
            var sessions = await sourceDb.Sessions.AsNoTracking()
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            foreach (var session in sessions)
            {
                var targetShard = store.ShardFor(session.AccountId);
                var target = store.AllOptions()[targetShard];
                if (string.Equals(ConnectionString(target), ConnectionString(source), StringComparison.Ordinal))
                {
                    continue;
                }

                scanned++;
                if (dryRun)
                {
                    moved++;
                    continue;
                }

                await using (var targetDb = new SessionDbContext(target))
                {
                    if (!await targetDb.Sessions.AnyAsync(x => x.Id == session.Id, cancellationToken))
                    {
                        targetDb.Sessions.Add(session);
                        await targetDb.SaveChangesAsync(cancellationToken);
                    }
                }

                await using (var deleteDb = new SessionDbContext(source))
                {
                    await deleteDb.Database.ExecuteSqlInterpolatedAsync(
                        $"delete from session.\"session\" where \"Id\" = {session.Id}", cancellationToken);
                }

                moved++;
            }
        }

        Reported(logger, scanned, moved, dryRun, null);

        return new SessionRebalanceReport(scanned, moved, dryRun);
    }

    private static string? ConnectionString(DbContextOptions<SessionDbContext> options)
    {
        using var db = new SessionDbContext(options);
        return db.Database.GetConnectionString();
    }

    internal static IReadOnlyList<DbContextOptions<SessionDbContext>> PreviousShards(IConfiguration configuration)
    {
        var count = configuration.GetValue("Sharding:PreviousShardCount", 0);
        if (count < 1)
        {
            throw new InvalidOperationException(
                "Sharding:PreviousShardCount must be at least 1 for a rebalance run.");
        }

        var result = new DbContextOptions<SessionDbContext>[count];
        for (var index = 0; index < count; index++)
        {
            var connection = configuration[$"Sharding:PreviousConnectionStrings:{index}"];
            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new InvalidOperationException(
                    $"Sharding:PreviousConnectionStrings:{index} is required for a rebalance run.");
            }

            result[index] = new DbContextOptionsBuilder<SessionDbContext>()
                .UseNpgsql(connection)
                .Options;
        }

        return result;
    }
}
