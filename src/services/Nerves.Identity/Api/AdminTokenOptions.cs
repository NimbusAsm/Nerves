namespace Nerves.Identity.Api;

/// <summary>
/// Static bearer token protecting the management API until the authorization
/// service owns admin access. Never enable an empty token outside development.
/// </summary>
public sealed class AdminTokenOptions
{
    public const string SectionName = "Api";

    public string? AdminToken { get; set; }
}
