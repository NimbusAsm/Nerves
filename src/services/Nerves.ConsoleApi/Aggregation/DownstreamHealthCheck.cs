using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nerves.ConsoleApi.Options;

namespace Nerves.ConsoleApi.Aggregation;

/// <summary>
/// Readiness view of the services the console depends on. Reported as
/// <see cref="HealthStatus.Degraded"/> rather than unhealthy: the console keeps
/// answering with warnings when a dependency is down.
/// </summary>
public sealed class DownstreamHealthCheck(IDownstreamClient downstream, ConsoleOptions options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var probes = options.Services.Keys
            .Select(name => ProbeAsync(name, cancellationToken))
            .ToArray();

        var results = await Task.WhenAll(probes).ConfigureAwait(false);

        var data = results.ToDictionary(
            result => result.Name,
            result => (object)(result.Reachable ? "ok" : "unreachable"),
            StringComparer.Ordinal);

        var failed = results.Where(result => !result.Reachable).Select(result => result.Name).ToArray();

        return failed.Length == 0
            ? HealthCheckResult.Healthy("all downstream services answered", data)
            : HealthCheckResult.Degraded(
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"{failed.Length} downstream service(s) unreachable: {string.Join(",", failed)}"),
                data: data);
    }

    private async Task<ServiceStatus> ProbeAsync(string name, CancellationToken cancellationToken)
    {
        var result = await downstream.GetAsync(name, "/healthz", cancellationToken).ConfigureAwait(false);
        return new ServiceStatus(name, result.IsSuccess, null, result.ElapsedMilliseconds, result.Warning);
    }
}
