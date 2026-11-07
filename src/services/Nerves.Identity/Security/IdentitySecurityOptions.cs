namespace Nerves.Identity.Security;

/// <summary>Keys used to protect identifiers and passwords.</summary>
public sealed class IdentitySecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Base64 encoded 32-byte key used to HMAC login identifiers.</summary>
    public string? IdentifierHashKey { get; set; }
}
