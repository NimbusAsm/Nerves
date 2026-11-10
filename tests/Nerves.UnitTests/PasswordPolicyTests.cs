using Nerves.Identity.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("short")]
    [InlineData("elevenchars")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_weak_passwords(string? password)
    {
        PasswordPolicy.Validate(password).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Accepts_a_twelve_character_password()
    {
        PasswordPolicy.Validate("twelve-chars").IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_overlong_passwords()
    {
        PasswordPolicy.Validate(new string('a', 257)).IsFailure.ShouldBeTrue();
    }
}
