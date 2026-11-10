namespace Nerves.Idp.Options;

/// <summary>Lifetimes shared by the session cookie and the session service.</summary>
public sealed class SessionLifetimeOptions
{
    public const string SectionName = "Session";

    public int IdleMinutes { get; set; } = 30;

    public int AbsoluteHours { get; set; } = 12;
}
