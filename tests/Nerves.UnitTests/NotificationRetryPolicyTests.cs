using Nerves.Notification.Domain;
using Nerves.Notification.Options;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class NotificationRetryPolicyTests
{
    private static RetryPolicy Policy() =>
        new(maxAttempts: 5, baseDelay: TimeSpan.FromSeconds(30), maxDelay: TimeSpan.FromMinutes(10));

    [Fact]
    public void The_delay_doubles_after_each_failure()
    {
        var policy = Policy();

        policy.DelayAfterFailure(1).ShouldBe(TimeSpan.FromSeconds(30));
        policy.DelayAfterFailure(2).ShouldBe(TimeSpan.FromSeconds(60));
        policy.DelayAfterFailure(3).ShouldBe(TimeSpan.FromSeconds(120));
        policy.DelayAfterFailure(4).ShouldBe(TimeSpan.FromSeconds(240));
    }

    [Fact]
    public void The_delay_is_capped()
    {
        var policy = new RetryPolicy(20, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));

        policy.DelayAfterFailure(10).ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Options_are_clamped_into_a_usable_policy()
    {
        var options = new DeliveryOptions
        {
            MaxAttempts = 0,
            BaseDelaySeconds = 0,
            MaxDelaySeconds = 0,
        };

        var policy = options.ToRetryPolicy();

        policy.MaxAttempts.ShouldBe(1);
        policy.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
        policy.MaxDelay.ShouldBeGreaterThanOrEqualTo(policy.BaseDelay);
    }

    [Fact]
    public void Invalid_construction_is_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new RetryPolicy(0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
        Should.Throw<ArgumentOutOfRangeException>(() => new RetryPolicy(3, TimeSpan.Zero, TimeSpan.FromSeconds(2)));
        Should.Throw<ArgumentOutOfRangeException>(() => new RetryPolicy(3, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1)));
        Should.Throw<ArgumentOutOfRangeException>(() => Policy().DelayAfterFailure(0));
    }
}
