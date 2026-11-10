using Nerves.Authorization.Domain;
using Nerves.Session.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class ServiceOutboxTests
{
    [Fact]
    public void Authorization_outbox_events_start_pending()
    {
        var now = DateTimeOffset.UtcNow;
        var item = Nerves.Authorization.Domain.OutboxEvent.Create(
            "authz.role.created", "role", Guid.NewGuid().ToString(), "{}", now);

        item.DispatchedAt.ShouldBeNull();
        item.Attempts.ShouldBe(0);
        item.Type.ShouldBe("authz.role.created");
        item.OccurredAt.ShouldBe(now);
    }

    [Fact]
    public void Session_outbox_events_count_attempts_without_dispatching_on_failure()
    {
        var now = DateTimeOffset.UtcNow;
        var item = Nerves.Session.Domain.OutboxEvent.Create("session.revoked", "session", "id", "{}", now);

        item.MarkAttempt();
        item.MarkAttempt();

        item.Attempts.ShouldBe(2);
        item.DispatchedAt.ShouldBeNull();
    }

    [Fact]
    public void Dispatching_marks_the_event_once()
    {
        var now = DateTimeOffset.UtcNow;
        var item = Nerves.Directory.Domain.OutboxEvent.Create("directory.member.added", "account", "id", "{}", now);

        item.MarkDispatched(now.AddSeconds(1));

        item.Attempts.ShouldBe(1);
        item.DispatchedAt.ShouldBe(now.AddSeconds(1));
    }
}
