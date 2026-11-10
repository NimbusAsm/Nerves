namespace Nerves.Session.Options;

/// <summary>Lifetimes for interactive sessions; shared vocabulary with the IdP.</summary>
public sealed class SessionLifetimeOptions
{
    public const string SectionName = "Session";

    /// <summary>Inactivity window after which the session expires.</summary>
    public int IdleMinutes { get; set; } = 30;

    /// <summary>Hard cap regardless of activity.</summary>
    public int AbsoluteHours { get; set; } = 12;

    /// <summary>How often expired sessions are marked revoked.</summary>
    public int ExpirySweepSeconds { get; set; } = 300;
}
