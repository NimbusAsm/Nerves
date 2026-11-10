namespace Nerves.Sharding;

/// <summary>One migration result for a shard.</summary>
public sealed record ShardMigrationResult(int Shard, bool Succeeded, string? Error);

/// <summary>
/// Applies a migration callback to every configured shard and reports per-shard
/// outcomes. Used by migration jobs that must cover a sharded deployment.
/// </summary>
public sealed class ShardMigrationRunner(ShardConnectionResolver connections)
{
    public async Task<IReadOnlyList<ShardMigrationResult>> RunAsync(
        Func<int, Task> applyAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(applyAsync);
        ArgumentNullException.ThrowIfNull(connections);

        var results = new List<ShardMigrationResult>(connections.ShardCount);
        for (var shard = 0; shard < connections.ShardCount; shard++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await applyAsync(shard);
                results.Add(new ShardMigrationResult(shard, true, null));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(new ShardMigrationResult(shard, false, exception.Message));
            }
        }

        return results;
    }
}
