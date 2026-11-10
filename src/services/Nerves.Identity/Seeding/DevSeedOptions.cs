namespace Nerves.Identity.Seeding;

/// <summary>
/// Creates a first administrator account on startup. Disabled by default; only
/// meant for local development and demos.
/// </summary>
public sealed class DevSeedOptions
{
    public const string SectionName = "DevSeed";

    public bool Enabled { get; set; }

    /// <summary>Explicit opt-in for non-Development environments.</summary>
    public bool AllowInProduction { get; set; }

    public string Realm { get; set; } = "nerves";

    public string Username { get; set; } = "admin";

    public string Password { get; set; } = "ChangeMe!123";

    public string Email { get; set; } = "admin@nerves.local";

    public string DisplayName { get; set; } = "Nerves Administrator";
}
