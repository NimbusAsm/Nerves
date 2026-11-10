using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Persistence;

namespace Nerves.Identity.Sharding;

/// <summary>Result of a routing index shard reconciliation pass.</summary>
public sealed record ShardReconciliation(int Scanned, int Changed, IReadOnlyDictionary<int, int> Distribution);

/// <summary>
/// Recomputes the stored shard for every routing index row. Needed whenever
/// Sharding:ShardCount changes: the shard is a pure function of the account id,
/// so rows written under the old count would route elsewhere.
/// </summary>
public sealed class RoutingIndexReconciler(IdentityDbContext db, ShardResolver shards)
{
    private const int BatchSize = 500;

    public async Task<ShardReconciliation> RunAsync(
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var scanned = 0;
        var changed = 0;
        var distribution = new Dictionary<int, int>();

        var lastId = string.Empty;
        while (true)
        {
            var batch = await db.RoutingIndex
                .Where(x => x.IdentifierHash.CompareTo(lastId) > 0)
                .OrderBy(x => x.IdentifierHash)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var entry in batch)
            {
                scanned++;
                var expected = shards.Resolve(entry.AccountId);
                distribution[expected] = distribution.GetValueOrDefault(expected) + 1;

                if (entry.Shard != expected && !dryRun)
                {
                    entry.Reshard(expected);
                    changed++;
                }
                else if (entry.Shard != expected)
                {
                    changed++;
                }
            }

            lastId = batch[^1].IdentifierHash;

            if (!dryRun)
            {
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
            }
        }

        return new ShardReconciliation(scanned, changed, distribution);
    }
}
