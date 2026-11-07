namespace Nerves.ServiceDefaults.Security;

/// <summary>
/// Static bearer token protecting management APIs until the authorization
/// service owns operator access. Never set an empty token in production.
/// </summary>
public sealed class AdminTokenOptions
{
    public const string SectionName = "Api";

    public string? AdminToken { get; set; }
}
