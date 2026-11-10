namespace Nerves.ComplianceCn.Options;

/// <summary>
/// Enforcement of the stored retention policies. Disabled by default: an
/// operator opts in per deployment once the policy table is correct.
/// </summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>When false the worker never deletes anything.</summary>
    public bool Enabled { get; set; }

    /// <summary>How often the sweep runs, in minutes. Clamped to [1, 1440].</summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>Seconds to wait before the first sweep so migrations settle.</summary>
    public int StartDelaySeconds { get; set; } = 10;

    /// <summary>Batch size per category; keeps one sweep bounded.</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>
    /// Policy category whose retention window applies to consent records.
    /// When the policy is missing the category is skipped, never guessed.
    /// </summary>
    public string ConsentCategory { get; set; } = "consent";

    /// <summary>Policy category whose retention window applies to erasure requests.</summary>
    public string ErasureCategory { get; set; } = "erasure-request";

    public int ClampInterval() => Math.Clamp(IntervalMinutes, 1, 1440);

    public int ClampBatch() => Math.Clamp(BatchSize, 1, 5000);
}
