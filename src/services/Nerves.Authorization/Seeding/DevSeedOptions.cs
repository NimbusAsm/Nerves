namespace Nerves.Authorization.Seeding;

/// <summary>
/// Development bootstrap: grants the management permission to listed accounts
/// so the admin console is usable before a real provisioning flow exists.
/// </summary>
public sealed class DevSeedOptions
{
    public const string SectionName = "DevSeed";

    public bool Enabled { get; set; }

    public string Realm { get; set; } = "nerves";

    public string RoleCode { get; set; } = "portal-admin";

    public string PermissionCode { get; set; } = "nerves:admin";

    public Guid[] AdminAccountIds { get; set; } = [];
}
