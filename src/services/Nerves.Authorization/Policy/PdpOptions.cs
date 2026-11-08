namespace Nerves.Authorization.Policy;

/// <summary>External policy decision point configuration.</summary>
public sealed class PdpOptions
{
    public const string SectionName = "Pdp";

    /// <summary>
    /// When set, every decision is delegated to this endpoint. The service
    /// posts <c>{ subject, legalEntityId, permission }</c> and expects
    /// <c>{ allowed: bool, reason?: string }</c>.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>Sent as a bearer token when configured.</summary>
    public string? Token { get; set; }

    public int TimeoutSeconds { get; set; } = 3;

    /// <summary>
    /// Fail closed by default: an unreachable PDP denies the decision instead of
    /// silently allowing it.
    /// </summary>
    public bool FailOpen { get; set; }
}
