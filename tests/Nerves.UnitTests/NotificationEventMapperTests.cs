using Nerves.Notification.Consumption;
using Nerves.Notification.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class NotificationEventMapperTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static EventEnvelope Event(string type, string payload = "{}") => new(
        Guid.CreateVersion7(Now),
        type,
        Guid.CreateVersion7(Now),
        null,
        payload,
        Now);

    private static AccountSnapshot Account(string? email = "user@example.com") => new(
        Guid.CreateVersion7(Now),
        email,
        "张伟",
        "zhang.wei");

    [Fact]
    public void A_created_account_maps_to_the_welcome_email()
    {
        var result = EventTemplateMapper.Map(
            Event(EventTemplateMapper.AccountCreated),
            Account(),
            Now,
            "https://nerves.example.com/");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Channel.ShouldBe(NotificationChannel.Email);
        result.Value.Recipient.ShouldBe("user@example.com");
        result.Value.Template.ShouldBe("account.welcome");
        result.Value.Values["displayName"].ShouldBe("张伟");
        result.Value.Values["username"].ShouldBe("zhang.wei");
        result.Value.Values["issuer"].ShouldBe("https://nerves.example.com/");
    }

    [Fact]
    public void A_real_name_change_maps_to_the_real_name_email_with_the_payload_state()
    {
        var result = EventTemplateMapper.Map(
            Event(EventTemplateMapper.RealnameChanged, "{\"state\":\"Verified\"}"),
            Account(),
            Now,
            "https://nerves.example.com/");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Template.ShouldBe("account.realname-changed");
        result.Value.Values["state"].ShouldBe("Verified");
        result.Value.Values["occurredAt"].ShouldBe("2026-11-10 12:00 UTC");
    }

    [Fact]
    public void A_malformed_payload_does_not_block_the_mapping()
    {
        var result = EventTemplateMapper.Map(
            Event(EventTemplateMapper.RealnameChanged, "not-json"),
            Account(),
            Now,
            "https://nerves.example.com/");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Values["state"].ShouldBe("unknown");
    }

    [Fact]
    public void An_unknown_event_type_is_reported_as_unsupported()
    {
        var result = EventTemplateMapper.Map(
            Event("account.deleted-by-aliens"),
            Account(),
            Now,
            "https://nerves.example.com/");

        result.IsFailure.ShouldBeTrue();
        result.Reason.Code.ShouldBe(EventTemplateMapper.UnsupportedEventCode);
    }

    [Fact]
    public void An_account_without_an_email_is_reported_as_missing_recipient()
    {
        var result = EventTemplateMapper.Map(
            Event(EventTemplateMapper.AccountCreated),
            Account(email: null),
            Now,
            "https://nerves.example.com/");

        result.IsFailure.ShouldBeTrue();
        result.Reason.Code.ShouldBe(EventTemplateMapper.MissingRecipientCode);
    }

    [Fact]
    public void The_display_name_falls_back_to_the_username()
    {
        var account = new AccountSnapshot(Guid.CreateVersion7(Now), "user@example.com", null, "zhang.wei");

        var result = EventTemplateMapper.Map(
            Event(EventTemplateMapper.AccountCreated),
            account,
            Now,
            "https://nerves.example.com/");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Values["displayName"].ShouldBe("zhang.wei");
    }
}
