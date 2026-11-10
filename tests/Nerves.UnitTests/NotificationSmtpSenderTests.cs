using Microsoft.Extensions.Options;
using Nerves.Notification.Delivery;
using Nerves.Notification.Options;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class NotificationSmtpSenderTests
{
    [Fact]
    public void A_missing_host_is_rejected_at_construction()
    {
        var options = Options.Create(new SmtpOptions
        {
            Disabled = false,
            Host = "   ",
        });

        Should.Throw<InvalidOperationException>(() => new SmtpEmailSender(options));
    }

    [Fact]
    public void A_configured_sender_does_not_connect_until_send_and_disposes_cleanly()
    {
        var options = Options.Create(new SmtpOptions
        {
            Disabled = false,
            Host = "127.0.0.1",
            Port = 2525,
            From = "noreply@nerves.local",
            Tls = false,
            TimeoutSeconds = 1,
        });

        var sender = new SmtpEmailSender(options);
        Should.NotThrow(sender.Dispose);
        Should.NotThrow(sender.Dispose);

        Should.Throw<ObjectDisposedException>(() =>
            sender.SendAsync(new EmailSendRequest("a@example.com", "subject", "body"), CancellationToken.None)
                .GetAwaiter()
                .GetResult());
    }

    [Fact]
    public void Disabling_smtp_makes_every_other_setting_optional()
    {
        new SmtpOptions { Disabled = true, Host = "" }.Validate().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_missing_host_fails_startup_validation()
    {
        var result = new SmtpOptions { Disabled = false, Host = " " }.Validate();

        result.IsFailure.ShouldBeTrue();
        result.Reason.Message.ShouldContain("Smtp:Host");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public void An_out_of_range_port_fails_startup_validation(int port)
    {
        var result = new SmtpOptions { Disabled = false, Host = "smtp.example.com", Port = port }.Validate();

        result.IsFailure.ShouldBeTrue();
        result.Reason.Message.ShouldContain("Smtp:Port");
    }

    [Fact]
    public void An_invalid_from_address_fails_startup_validation()
    {
        var result = new SmtpOptions { Disabled = false, Host = "smtp.example.com", From = "not-an-address" }.Validate();

        result.IsFailure.ShouldBeTrue();
        result.Reason.Message.ShouldContain("Smtp:From");
    }

    [Fact]
    public void A_complete_configuration_passes_validation()
    {
        new SmtpOptions
        {
            Disabled = false,
            Host = "smtp.example.com",
            Port = 587,
            From = "noreply@example.com",
            TimeoutSeconds = 30,
        }.Validate().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void The_noop_sender_reports_success()
    {
        var sender = new NoOpEmailSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<NoOpEmailSender>.Instance);

        Should.NotThrow(() =>
            sender.SendAsync(new EmailSendRequest("a@example.com", "subject", "body"), CancellationToken.None)
                .GetAwaiter()
                .GetResult());
    }
}
