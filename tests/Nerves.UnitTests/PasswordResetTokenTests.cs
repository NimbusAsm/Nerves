using Nerves.Identity.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class PasswordResetTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly string Hash = new('a', 64);

    [Fact]
    public void A_fresh_token_is_usable_until_it_expires()
    {
        var token = PasswordResetToken.Issue(Guid.NewGuid(), Hash, Now, TimeSpan.FromMinutes(15)).Value;

        token.IsUsable(Now).ShouldBeTrue();
        token.IsUsable(Now.AddMinutes(14)).ShouldBeTrue();
        token.IsUsable(Now.AddMinutes(15)).ShouldBeFalse();
        token.ConsumedAt.ShouldBeNull();
    }

    [Fact]
    public void Consuming_a_token_marks_it_spent_and_blocks_a_second_use()
    {
        var token = PasswordResetToken.Issue(Guid.NewGuid(), Hash, Now, TimeSpan.FromMinutes(15)).Value;

        token.Consume(Now.AddMinutes(1)).IsSuccess.ShouldBeTrue();
        token.ConsumedAt.ShouldBe(Now.AddMinutes(1));
        token.IsUsable(Now.AddMinutes(2)).ShouldBeFalse();

        var replay = token.Consume(Now.AddMinutes(3));
        replay.IsFailure.ShouldBeTrue();
        replay.Reason.Code.ShouldBe("conflict");
    }

    [Fact]
    public void An_expired_token_cannot_be_consumed()
    {
        var token = PasswordResetToken.Issue(Guid.NewGuid(), Hash, Now, TimeSpan.FromMinutes(1)).Value;

        var result = token.Consume(Now.AddMinutes(2));

        result.IsFailure.ShouldBeTrue();
        token.ConsumedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void A_malformed_hash_is_rejected(string hash)
    {
        PasswordResetToken.Issue(Guid.NewGuid(), hash, Now, TimeSpan.FromMinutes(15)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void A_lifetime_outside_the_allowed_window_is_rejected()
    {
        PasswordResetToken.Issue(Guid.NewGuid(), Hash, Now, TimeSpan.Zero).IsFailure.ShouldBeTrue();
        PasswordResetToken.Issue(Guid.NewGuid(), Hash, Now, TimeSpan.FromHours(25)).IsFailure.ShouldBeTrue();
        PasswordResetToken.Issue(Guid.Empty, Hash, Now, TimeSpan.FromMinutes(15)).IsFailure.ShouldBeTrue();
    }
}
