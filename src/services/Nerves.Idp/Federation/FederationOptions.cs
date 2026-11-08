namespace Nerves.Idp.Federation;

/// <summary>One upstream OpenID Connect provider that can authenticate users.</summary>
public sealed class FederationProvider
{
    /// <summary>Short name clients put in <c>provider</c>.</summary>
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string Scope { get; set; } = "openid profile email";

    /// <summary>
    /// When false the identity is only accepted if an account already exists;
    /// no implicit provisioning happens.
    /// </summary>
    public bool AllowSignUp { get; set; }
}

/// <summary>Federation configuration.</summary>
public sealed class FederationOptions
{
    public const string SectionName = "Federation";

    public List<FederationProvider> Providers { get; set; } = [];
}
