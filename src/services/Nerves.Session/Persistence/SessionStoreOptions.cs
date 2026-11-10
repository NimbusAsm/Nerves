namespace Nerves.Session.Persistence;

/// <summary>Sharding toggle for the session data plane.</summary>
public sealed class SessionStoreOptions
{
    public const string SectionName = "Sharding";

    /// <summary>
    /// When false the service uses ConnectionStrings:Default exactly as before.
    /// When true every shard connection must be configured.
    /// </summary>
    public bool Enabled { get; set; }
}
