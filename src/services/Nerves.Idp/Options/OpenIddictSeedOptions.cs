namespace Nerves.Idp.Options;

/// <summary>Development-time client registration, applied on startup.</summary>
public sealed class OpenIddictSeedOptions
{
    public const string SectionName = "OpenIddictSeed";

    public bool Enabled { get; set; }

    /// <summary>Explicit opt-in for non-Development environments.</summary>
    public bool AllowInProduction { get; set; }

    public string ClientId { get; set; } = "nerves-demo";

    public string DisplayName { get; set; } = "Nerves demo client";

    public string[] RedirectUris { get; set; } = ["http://127.0.0.1:5100/signin-oidc"];

    public string[] PostLogoutRedirectUris { get; set; } = ["http://127.0.0.1:5100/signout-callback-oidc"];

    /// <summary>Confidential-free admin console client (SPA, PKCE).</summary>
    public string AdminClientId { get; set; } = "nerves-admin";

    public string AdminDisplayName { get; set; } = "Nerves admin console";

    public string[] AdminRedirectUris { get; set; } = ["http://127.0.0.1:5300/auth/callback"];

    public string[] AdminPostLogoutRedirectUris { get; set; } = ["http://127.0.0.1:5300/"];
}
