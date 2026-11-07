namespace Nerves.Identity.Options;

/// <summary>Where the audit service lives.</summary>
public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    public string GrpcAddress { get; set; } = "http://127.0.0.1:5205";
}
