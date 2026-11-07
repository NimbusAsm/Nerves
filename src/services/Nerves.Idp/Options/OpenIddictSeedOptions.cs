namespace Nerves.Idp.Options;

/// <summary>Development-time client registration, applied on startup.</summary>
public sealed class OpenIddictSeedOptions
{
    public const string SectionName = "OpenIddictSeed";

    public bool Enabled { get; set; }

    public string ClientId { get; set; } = "nerves-demo";

    public string DisplayName { get; set; } = "Nerves demo client";

    public string[] RedirectUris { get; set; } = ["http://127.0.0.1:5100/signin-oidc"];

    public string[] PostLogoutRedirectUris { get; set; } = ["http://127.0.0.1:5100/signout-callback-oidc"];
}
