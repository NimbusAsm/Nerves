using Microsoft.EntityFrameworkCore;

namespace Nerves.Identity.Persistence;

/// <summary>
/// Applies the identity migrations to the global database and every account
/// shard. Each database receives the full schema: tables that the deployment
/// does not use on that node are harmless and keep one migration history.
/// </summary>
public sealed class IdentityMigrator(IdentityDataPlane plane)
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plane);

        await using (var global = plane.CreateGlobal())
        {
            await global.Database.MigrateAsync(cancellationToken);
        }

        for (var index = 0; index < plane.ShardCount; index++)
        {
            await using var shard = plane.CreateShard(index);
            await shard.Database.MigrateAsync(cancellationToken);
        }
    }
}
