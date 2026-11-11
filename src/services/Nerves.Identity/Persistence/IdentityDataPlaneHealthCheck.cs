using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Nerves.Identity.Persistence;

/// <summary>Checks the global database and every configured account shard.</summary>
public sealed class IdentityDataPlaneHealthCheck(IdentityDataPlane plane) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(plane);

        var unreachable = new List<string>();

        await using (var global = plane.CreateGlobal())
        {
            if (!await global.Database.CanConnectAsync(cancellationToken))
            {
                unreachable.Add("global");
            }
        }

        for (var index = 0; index < plane.ShardCount; index++)
        {
            await using var shard = plane.CreateShard(index);
            if (!await shard.Database.CanConnectAsync(cancellationToken))
            {
                unreachable.Add("shard-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return unreachable.Count == 0
            ? HealthCheckResult.Healthy(
                plane.IsSharded ? "global and all account shards reachable" : "identity database reachable")
            : HealthCheckResult.Unhealthy("unreachable identity databases: " + string.Join(",", unreachable));
    }
}
