using Nerves.Identity.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class PasskeyChallengeTests
{
    [Fact]
    public void A_challenge_is_usable_until_consumed()
    {
        var challenge = PasskeyChallenge.Create(
            Guid.CreateVersion7(), "abc", "registration", "{}", TimeSpan.FromMinutes(5));

        challenge.ConsumedAt.ShouldBeNull();
        challenge.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        challenge.Consume();

        challenge.ConsumedAt.ShouldNotBeNull();
    }

    [Fact]
    public void The_purpose_separates_registration_from_assertion()
    {
        var registration = PasskeyChallenge.Create(
            Guid.CreateVersion7(), "abc", "registration", "{}", TimeSpan.FromMinutes(5));

        registration.Purpose.ShouldBe("registration");
        registration.OptionsJson.ShouldBe("{}");
    }
}
