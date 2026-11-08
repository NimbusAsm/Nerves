namespace Nerves.Audit.Archiving;

/// <summary>Configuration for the append-only audit archive.</summary>
public sealed class ArchiveOptions
{
    public const string SectionName = "Archive";

    /// <summary>Directory that receives the daily NDJSON files.</summary>
    public string Directory { get; set; } = "audit-archive";

    /// <summary>Maximum events handled per pass.</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>Seconds between background passes; zero disables the timer.</summary>
    public int IntervalSeconds { get; set; } = 60;
}
