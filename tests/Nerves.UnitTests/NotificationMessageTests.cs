using Nerves.Notification.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class NotificationMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly EmailAddress Recipient = EmailAddress.Create("user@example.com").Value;

    private static readonly RetryPolicy Policy =
        new(maxAttempts: 3, baseDelay: TimeSpan.FromMinutes(1), maxDelay: TimeSpan.FromMinutes(10));

    private static NotificationMessage Create() =>
        NotificationMessage.Create(Recipient, "account.welcome", "欢迎", "正文", Now);

    [Fact]
    public void A_new_message_is_pending_and_due_immediately()
    {
        var message = Create();

        message.Status.ShouldBe(NotificationStatus.Pending);
        message.Status.ToWire().ShouldBe("pending");
        message.Attempts.ShouldBe(0);
        message.SentAt.ShouldBeNull();
        message.CreatedAt.ShouldBe(Now);
        message.IsDue(Now).ShouldBeTrue();
    }

    [Fact]
    public void Sending_marks_the_message_as_sent()
    {
        var message = Create();

        message.MarkSent(Now.AddSeconds(5));

        message.Status.ShouldBe(NotificationStatus.Sent);
        message.Status.ToWire().ShouldBe("sent");
        message.SentAt.ShouldBe(Now.AddSeconds(5));
        message.Error.ShouldBeNull();
        message.IsDue(Now.AddMinutes(5)).ShouldBeFalse();
    }

    [Fact]
    public void A_failure_schedules_a_backoff_retry()
    {
        var message = Create();

        message.RecordFailure("smtp refused", Now, Policy);

        message.Status.ShouldBe(NotificationStatus.Pending);
        message.Attempts.ShouldBe(1);
        message.Error.ShouldBe("smtp refused");
        message.NextAttemptAt.ShouldBe(Now.AddMinutes(1));
        message.IsDue(Now.AddSeconds(30)).ShouldBeFalse();
        message.IsDue(Now.AddMinutes(1)).ShouldBeTrue();
    }

    [Fact]
    public void Retries_stop_after_the_configured_attempts()
    {
        var message = Create();
        var moment = Now;

        for (var attempt = 1; attempt <= 3; attempt += 1)
        {
            message.RecordFailure("smtp refused", moment, Policy);
            moment = moment.AddMinutes(attempt * 2);
        }

        message.Attempts.ShouldBe(3);
        message.Status.ShouldBe(NotificationStatus.Failed);
        message.Status.ToWire().ShouldBe("failed");
        message.IsDue(moment.AddDays(1)).ShouldBeFalse();
    }

    [Fact]
    public void A_success_after_a_failure_clears_the_error()
    {
        var message = Create();

        message.RecordFailure("temporary", Now, Policy);
        message.MarkSent(Now.AddMinutes(1));

        message.Status.ShouldBe(NotificationStatus.Sent);
        message.Error.ShouldBeNull();
    }

    [Fact]
    public void Long_errors_are_truncated_to_the_column_limit()
    {
        var message = Create();

        message.RecordFailure(new string('x', NotificationMessage.MaxErrorLength + 100), Now, Policy);

        message.Error!.Length.ShouldBe(NotificationMessage.MaxErrorLength);
    }

    [Fact]
    public void The_status_wire_format_is_stable()
    {
        NotificationStatus.Pending.ToWire().ShouldBe("pending");
        NotificationStatus.Sent.ToWire().ShouldBe("sent");
        NotificationStatus.Failed.ToWire().ShouldBe("failed");
    }
}
