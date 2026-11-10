using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Nerves.Session.Persistence;

/// <summary>Checks every configured session shard.</summary>
public sealed class SessionStoreHealthCheck(SessionStore store) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var unreachable = new List<int>();
        for (var shard = 0; shard < store.ShardCount; shard++)
        {
            await using var db = new SessionDbContext(store.AllOptions()[shard]);
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                unreachable.Add(shard);
            }
        }

        return unreachable.Count == 0
            ? HealthCheckResult.Healthy("all session shards reachable")
            : HealthCheckResult.Unhealthy("unreachable shards: " + string.Join(",", unreachable));
    }
}
