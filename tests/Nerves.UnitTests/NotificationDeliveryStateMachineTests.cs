using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Nerves.Notification.Delivery;
using Nerves.Notification.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class NotificationDeliveryStateMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly RetryPolicy Policy =
        new(maxAttempts: 3, baseDelay: TimeSpan.FromSeconds(30), maxDelay: TimeSpan.FromMinutes(5));

    private static NotificationMessage Create() =>
        NotificationMessage.Create(
            EmailAddress.Create("user@example.com").Value,
            "account.welcome",
            "欢迎",
            "正文",
            Now);

    private static DeliveryAttemptRunner Runner(IEmailSender sender) =>
        new(sender, NullLogger<DeliveryAttemptRunner>.Instance);

    [Fact]
    public async Task A_sender_that_accepts_the_message_marks_it_sent()
    {
        var message = Create();
        var runner = Runner(new FakeSender());

        var outcome = await runner.DeliverAsync(message, Policy, Now, CancellationToken.None);

        outcome.ShouldBe(DeliveryOutcome.Sent);
        message.Status.ShouldBe(NotificationStatus.Sent);
        message.SentAt.ShouldBe(Now);
        message.Attempts.ShouldBe(0);
        message.Error.ShouldBeNull();
    }

    [Fact]
    public async Task A_failure_schedules_the_next_retry_and_increments_attempts()
    {
        var message = Create();
        var runner = Runner(new FakeSender(new InvalidOperationException("first")));

        var outcome = await runner.DeliverAsync(message, Policy, Now, CancellationToken.None);

        outcome.ShouldBe(DeliveryOutcome.RetryScheduled);
        message.Status.ShouldBe(NotificationStatus.Pending);
        message.Attempts.ShouldBe(1);
        message.Error.ShouldBe("first");
        message.NextAttemptAt.ShouldBe(Now.AddSeconds(30));
    }

    [Fact]
    public async Task The_message_fails_after_the_attempt_budget_is_exhausted()
    {
        var message = Create();
        var runner = Runner(new FakeSender(new InvalidOperationException("smtp down")));

        var first = await runner.DeliverAsync(message, Policy, Now, CancellationToken.None);
        var second = await runner.DeliverAsync(message, Policy, message.NextAttemptAt, CancellationToken.None);
        var third = await runner.DeliverAsync(message, Policy, message.NextAttemptAt, CancellationToken.None);

        first.ShouldBe(DeliveryOutcome.RetryScheduled);
        second.ShouldBe(DeliveryOutcome.RetryScheduled);
        third.ShouldBe(DeliveryOutcome.Failed);
        message.Status.ShouldBe(NotificationStatus.Failed);
        message.Attempts.ShouldBe(3);
        message.Error.ShouldBe("smtp down");
        message.SentAt.ShouldBeNull();
        message.IsDue(Now.AddDays(1)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_retry_after_recovery_succeeds_and_clears_the_error()
    {
        var sender = new FakeSender(new InvalidOperationException("temporary"), failuresBeforeSuccess: 1);
        var message = Create();
        var runner = Runner(sender);

        var first = await runner.DeliverAsync(message, Policy, Now, CancellationToken.None);
        first.ShouldBe(DeliveryOutcome.RetryScheduled);

        var outcome = await runner.DeliverAsync(message, Policy, message.NextAttemptAt, CancellationToken.None);

        outcome.ShouldBe(DeliveryOutcome.Sent);
        message.Status.ShouldBe(NotificationStatus.Sent);
        message.Error.ShouldBeNull();
        message.Attempts.ShouldBe(1);
        sender.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task Cancellation_is_not_recorded_as_a_delivery_failure()
    {
        var message = Create();
        var sender = new FakeSender(failure: new OperationCanceledException("shutdown"));
        var runner = Runner(sender);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => runner.DeliverAsync(message, Policy, Now, cancellation.Token));

        // A shutting-down service must not burn retry budget on a cancelled send.
        message.Attempts.ShouldBe(0);
        message.Status.ShouldBe(NotificationStatus.Pending);
        message.Error.ShouldBeNull();
    }

    [Fact]
    public void Nested_smtp_errors_are_flattened_for_operators()
    {
        var inner = new SocketException((int)SocketError.ConnectionRefused);
        var outer = new InvalidOperationException("Failure sending mail.", inner);

        DeliveryAttemptRunner.Describe(outer).ShouldBe("Failure sending mail.: Connection refused");
    }

    [Fact]
    public void An_exception_without_a_message_still_records_something()
    {
        DeliveryAttemptRunner.Describe(new InvalidOperationException("   ")).ShouldBe("delivery failed");
    }

    private sealed class FakeSender : IEmailSender
    {
        private readonly Exception? _failure;
        private readonly int _failuresBeforeSuccess;

        /// <param name="failure">Exception thrown by a failing attempt.</param>
        /// <param name="failuresBeforeSuccess">How many attempts fail before one succeeds.</param>
        public FakeSender(Exception? failure = null, int failuresBeforeSuccess = int.MaxValue)
        {
            _failure = failure;
            _failuresBeforeSuccess = failuresBeforeSuccess;
        }

        public int Calls { get; private set; }

        public Task SendAsync(EmailSendRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Calls += 1;

            cancellationToken.ThrowIfCancellationRequested();

            if (_failure is not null && Calls <= _failuresBeforeSuccess)
            {
                throw _failure;
            }

            return Task.CompletedTask;
        }
    }
}
