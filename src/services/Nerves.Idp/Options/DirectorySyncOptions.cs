namespace Nerves.Idp.Options;

/// <summary>
/// Periodic provisioning from an LDAP directory into the identity service.
/// Connection settings are shared with <c>Ldap</c>; this section only decides
/// what to read and how often.
/// </summary>
public sealed class DirectorySyncOptions
{
    public const string SectionName = "Ldap:Sync";

    /// <summary>When false the worker stays idle and the status endpoint reports "disabled".</summary>
    public bool Enabled { get; set; }

    /// <summary>Minutes between two runs. Clamped to [1, 1440].</summary>
    public int IntervalMinutes { get; set; } = 15;

    /// <summary>Seconds to wait before the first run so dependencies can settle.</summary>
    public int StartDelaySeconds { get; set; } = 5;

    /// <summary>Realm that receives the provisioned accounts.</summary>
    public string Realm { get; set; } = "nerves";

    /// <summary>Search root; falls back to <c>Ldap:BaseDn</c> when empty.</summary>
    public string? BaseDn { get; set; }

    /// <summary>LDAP filter applied below the search root.</summary>
    public string Filter { get; set; } = "(objectClass=inetOrgPerson)";

    /// <summary>Simple-paged-results page size.</summary>
    public int PageSize { get; set; } = 200;

    /// <summary>Directory attributes that carry the provisioned fields.</summary>
    public DirectoryAttributeMap AttributeMap { get; set; } = new();

    public string ResolveBaseDn(string fallback) =>
        string.IsNullOrWhiteSpace(BaseDn) ? fallback : BaseDn.Trim();

    public string ResolveFilter() => string.IsNullOrWhiteSpace(Filter) ? "(objectClass=inetOrgPerson)" : Filter.Trim();
}

/// <summary>Attribute names used when mapping a directory entry to an account.</summary>
public sealed class DirectoryAttributeMap
{
    public string Username { get; set; } = "uid";

    public string Email { get; set; } = "mail";

    public string DisplayName { get; set; } = "cn";
}
