using Nerves.Identity.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class OutboxEventTests
{
    [Fact]
    public void A_new_event_is_pending()
    {
        var now = DateTimeOffset.UtcNow;
        var item = OutboxEvent.Create("realname.changed", Guid.NewGuid(), Guid.NewGuid(), "{}", now);

        item.DispatchedAt.ShouldBeNull();
        item.Attempts.ShouldBe(0);
        item.OccurredAt.ShouldBe(now);
    }

    [Fact]
    public void Dispatching_marks_the_timestamp_and_counts_the_attempt()
    {
        var now = DateTimeOffset.UtcNow;
        var item = OutboxEvent.Create("realname.changed", null, Guid.NewGuid(), "{}", now);

        item.MarkDispatched(now.AddSeconds(1));

        item.DispatchedAt.ShouldBe(now.AddSeconds(1));
        item.Attempts.ShouldBe(1);
    }

    [Fact]
    public void A_failed_attempt_increments_but_does_not_dispatch()
    {
        var now = DateTimeOffset.UtcNow;
        var item = OutboxEvent.Create("realname.changed", null, Guid.NewGuid(), "{}", now);

        item.MarkAttempt(now.AddSeconds(1));

        item.DispatchedAt.ShouldBeNull();
        item.Attempts.ShouldBe(1);
    }
}
