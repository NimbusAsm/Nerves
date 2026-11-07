namespace Nerves.Idp.Options;

/// <summary>Where the identity service lives.</summary>
public sealed class IdentityClientOptions
{
    public const string SectionName = "Identity";

    public string GrpcAddress { get; set; } = "http://127.0.0.1:5102";
}
