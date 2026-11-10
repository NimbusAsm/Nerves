using Nerves.Notification.Delivery;
using Nerves.Notification.Domain;
using Nerves.Notification.Templates;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class NotificationChannelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static NotificationMessage EmailMessage() =>
        NotificationMessage.Create(
            EmailAddress.Create("user@example.com").Value,
            "account.welcome",
            "欢迎",
            "正文",
            Now);

    private static NotificationMessage SmsMessage() =>
        NotificationMessage.Create(
            NotificationChannel.Sms,
            "+8613800138000",
            "security.sms-code",
            "验证码",
            "您的验证码是 123456",
            Now,
            Guid.CreateVersion7(Now));

    [Fact]
    public void A_message_created_from_an_email_defaults_to_the_email_channel()
    {
        var message = EmailMessage();

        message.Channel.ShouldBe(NotificationChannel.Email);
        message.Channel.ToWire().ShouldBe("email");
        message.SourceEventId.ShouldBeNull();
        message.To.ShouldBe("user@example.com");
    }

    [Fact]
    public void An_sms_message_keeps_its_channel_and_source_event()
    {
        var sourceEventId = Guid.CreateVersion7(Now);

        var message = NotificationMessage.Create(
            NotificationChannel.Sms,
            "+8613800138000",
            "security.sms-code",
            "验证码",
            "您的验证码是 123456",
            Now,
            sourceEventId);

        message.Channel.ShouldBe(NotificationChannel.Sms);
        message.Channel.ToWire().ShouldBe("sms");
        message.SourceEventId.ShouldBe(sourceEventId);
        message.To.ShouldBe("+8613800138000");
    }

    [Fact]
    public void An_unknown_channel_is_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => NotificationMessage.Create(
            (NotificationChannel)99,
            "user@example.com",
            "account.welcome",
            "欢迎",
            "正文",
            Now));
    }

    [Fact]
    public void Channel_wire_names_round_trip()
    {
        NotificationChannelWire.TryParse("EMAIL", out var email).ShouldBeTrue();
        email.ShouldBe(NotificationChannel.Email);

        NotificationChannelWire.TryParse(" sms ", out var sms).ShouldBeTrue();
        sms.ShouldBe(NotificationChannel.Sms);

        NotificationChannelWire.TryParse("carrier-pigeon", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task The_router_delivers_through_the_sender_registered_for_the_channel()
    {
        var email = new FakeMessageSender(NotificationChannel.Email);
        var sms = new FakeMessageSender(NotificationChannel.Sms);
        IMessageSender[] senders = [email, sms];
        var router = new MessageSenderRouter(senders);

        router.CanSend(NotificationChannel.Sms).ShouldBeTrue();
        await router.SendAsync(SmsMessage(), CancellationToken.None);

        sms.Messages.Count.ShouldBe(1);
        sms.Messages[0].Body.ShouldBe("您的验证码是 123456");
        email.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_router_refuses_a_channel_without_a_sender()
    {
        IMessageSender[] senders = [new FakeMessageSender(NotificationChannel.Email)];
        var router = new MessageSenderRouter(senders);

        router.CanSend(NotificationChannel.Sms).ShouldBeFalse();

        await Should.ThrowAsync<InvalidOperationException>(
            () => router.SendAsync(SmsMessage(), CancellationToken.None));
    }

    [Fact]
    public async Task The_email_adapter_passes_subject_and_body_through()
    {
        var inner = new FakeEmailSender();
        var sender = new EmailMessageSender(inner);

        await sender.SendAsync(
            new MessageSendRequest(NotificationChannel.Email, "user@example.com", "主题", "正文"),
            CancellationToken.None);

        inner.Requests.Count.ShouldBe(1);
        inner.Requests[0].Subject.ShouldBe("主题");
        inner.Requests[0].Body.ShouldBe("正文");
    }

    [Fact]
    public async Task The_sms_adapter_sends_the_body_only()
    {
        var inner = new FakeSmsSender();
        var sender = new SmsMessageSender(inner);

        await sender.SendAsync(
            new MessageSendRequest(NotificationChannel.Sms, "+8613800138000", "ignored", "验证码"),
            CancellationToken.None);

        inner.Requests.Count.ShouldBe(1);
        inner.Requests[0].To.ShouldBe("+8613800138000");
        inner.Requests[0].Body.ShouldBe("验证码");
    }

    [Fact]
    public void The_sms_code_template_stays_inside_one_segment()
    {
        var rendered = EmailTemplateCatalog.Render("security.sms-code", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["code"] = "123456",
            ["minutes"] = "5",
        });

        rendered.IsSuccess.ShouldBeTrue();
        rendered.Value.Body.ShouldContain("123456");
        rendered.Value.Body.Length.ShouldBeLessThan(160);
    }

    private sealed class FakeMessageSender(NotificationChannel channel) : IMessageSender
    {
        public NotificationChannel Channel { get; } = channel;

        public List<MessageSendRequest> Messages { get; } = [];

        public Task SendAsync(MessageSendRequest request, CancellationToken cancellationToken)
        {
            Messages.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailSendRequest> Requests { get; } = [];

        public Task SendAsync(EmailSendRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSmsSender : ISmsSender
    {
        public List<SmsSendRequest> Requests { get; } = [];

        public Task SendAsync(SmsSendRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }
}
