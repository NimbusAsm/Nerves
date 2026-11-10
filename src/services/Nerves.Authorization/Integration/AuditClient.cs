namespace Nerves.Authorization.Integration;

/// <summary>Audit options for the authorization service.</summary>
public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    public string GrpcAddress { get; set; } = "http://127.0.0.1:5205";
}
