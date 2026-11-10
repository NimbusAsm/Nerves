using Nerves.Notification.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class NotificationEmailAddressTests
{
    [Fact]
    public void A_plain_address_is_normalised_to_lower_case()
    {
        var result = EmailAddress.Create("  Zhang.Wei@Example.COM  ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("zhang.wei@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-address")]
    [InlineData("a@")]
    [InlineData("@example.com")]
    [InlineData("a b@example.com")]
    public void Malformed_addresses_are_rejected(string candidate)
    {
        EmailAddress.Create(candidate).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void A_display_name_is_rejected_so_headers_cannot_be_forged()
    {
        var result = EmailAddress.Create("Nerves <a@example.com>");

        result.IsFailure.ShouldBeTrue();
        result.Reason.Code.ShouldBe("validation.failed");
    }

    [Fact]
    public void Line_breaks_are_rejected()
    {
        EmailAddress.Create("a@example.com\r\nBcc: victim@example.com").IsFailure.ShouldBeTrue();
        EmailAddress.Create("a@example.com\nBcc: victim@example.com").IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void An_overlong_address_is_rejected()
    {
        var local = new string('a', 310);
        EmailAddress.Create(local + "@example.com").IsFailure.ShouldBeTrue();
    }
}
