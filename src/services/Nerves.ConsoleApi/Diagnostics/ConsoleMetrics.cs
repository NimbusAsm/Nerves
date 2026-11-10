using System.Diagnostics.Metrics;

namespace Nerves.ConsoleApi.Diagnostics;

/// <summary>Metrics specific to the aggregation layer.</summary>
public static class ConsoleMetrics
{
    public const string MeterName = "Nerves.Console";

    private static readonly Meter Meter = new(MeterName);

    /// <summary>Downstream calls that did not return a usable 2xx answer.</summary>
    public static readonly Counter<long> DownstreamFailures = Meter.CreateCounter<long>(
        "console.downstream.failures",
        unit: "{call}",
        description: "Downstream calls the console could not complete.");

    /// <summary>Wall-clock duration of one downstream call.</summary>
    public static readonly Histogram<double> DownstreamDuration = Meter.CreateHistogram<double>(
        "console.downstream.duration",
        unit: "ms",
        description: "Duration of a single downstream HTTP call.");
}
