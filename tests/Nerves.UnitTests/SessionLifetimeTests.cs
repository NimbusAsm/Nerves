using Shouldly;
using DomainSession = Nerves.Session.Domain.Session;

namespace Nerves.UnitTests;

public sealed class SessionLifetimeTests
{
    private static DomainSession Create(DateTimeOffset now, TimeSpan idle, TimeSpan absolute) =>
        DomainSession.Create(
            Guid.NewGuid(),
            null,
            "nerves",
            "device",
            "iphash",
            "uahash",
            ["pwd"],
            now,
            idle,
            absolute);

    [Fact]
    public void A_fresh_session_is_active()
    {
        var now = DateTimeOffset.UtcNow;
        var session = Create(now, TimeSpan.FromMinutes(30), TimeSpan.FromHours(12));

        session.IsActive(now).ShouldBeTrue();
    }

    [Fact]
    public void An_idle_session_expires()
    {
        var now = DateTimeOffset.UtcNow;
        var session = Create(now, TimeSpan.FromMinutes(30), TimeSpan.FromHours(12));

        session.IsActive(now.AddMinutes(31)).ShouldBeFalse();
    }

    [Fact]
    public void The_absolute_cap_holds_even_after_touch()
    {
        var now = DateTimeOffset.UtcNow;
        var session = Create(now, TimeSpan.FromMinutes(30), TimeSpan.FromHours(12));

        session.Touch(now.AddHours(11));

        session.IsActive(now.AddHours(13)).ShouldBeFalse();
    }

    [Fact]
    public void A_revoked_session_is_inactive()
    {
        var now = DateTimeOffset.UtcNow;
        var session = Create(now, TimeSpan.FromMinutes(30), TimeSpan.FromHours(12));

        session.Revoke("logout", now);

        session.IsActive(now).ShouldBeFalse();
    }
}
