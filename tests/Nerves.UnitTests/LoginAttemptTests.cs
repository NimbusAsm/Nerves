using Nerves.Idp.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class LoginAttemptTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    [Fact]
    public void Five_failures_lock_the_identifier()
    {
        var now = DateTimeOffset.UtcNow;
        var attempt = LoginAttempt.Create("hash", now);

        for (var i = 0; i < 4; i++) attempt.RegisterFailure(now, 5, Window, Lockout);
        attempt.IsLocked(now).ShouldBeFalse();

        attempt.RegisterFailure(now, 5, Window, Lockout);
        attempt.IsLocked(now).ShouldBeTrue();
    }

    [Fact]
    public void A_lock_expires()
    {
        var now = DateTimeOffset.UtcNow;
        var attempt = LoginAttempt.Create("hash", now);

        for (var i = 0; i < 5; i++) attempt.RegisterFailure(now, 5, Window, Lockout);

        attempt.IsLocked(now.AddMinutes(16)).ShouldBeFalse();
    }

    [Fact]
    public void Failures_outside_the_window_start_over()
    {
        var now = DateTimeOffset.UtcNow;
        var attempt = LoginAttempt.Create("hash", now);

        for (var i = 0; i < 4; i++) attempt.RegisterFailure(now, 5, Window, Lockout);
        attempt.RegisterFailure(now.AddMinutes(20), 5, Window, Lockout);

        attempt.IsLocked(now.AddMinutes(21)).ShouldBeFalse();
    }

    [Fact]
    public void A_successful_login_clears_the_counter()
    {
        var now = DateTimeOffset.UtcNow;
        var attempt = LoginAttempt.Create("hash", now);

        for (var i = 0; i < 5; i++) attempt.RegisterFailure(now, 5, Window, Lockout);
        attempt.Reset();

        attempt.IsLocked(now).ShouldBeFalse();
        attempt.Failures.ShouldBe(0);
    }
}
