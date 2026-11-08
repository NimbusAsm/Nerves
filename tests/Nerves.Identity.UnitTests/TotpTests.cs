using Nerves.Identity.Domain;
using Nerves.Identity.Security;
using OtpNet;
using Shouldly;

namespace Nerves.Identity.UnitTests;

public sealed class TotpTests
{
    private readonly TotpService _totp = new();

    [Fact]
    public void Generates_a_base32_secret()
    {
        var secret = _totp.GenerateSecret();

        secret.ShouldNotBeNullOrWhiteSpace();
        secret.Length.ShouldBeGreaterThanOrEqualTo(32);
        secret.All(c => char.IsLetterOrDigit(c) || c == '=').ShouldBeTrue();
    }

    [Fact]
    public void Verifies_the_current_code_and_rejects_a_wrong_one()
    {
        var secret = _totp.GenerateSecret();
        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

        _totp.Verify(secret, code).ShouldBeTrue();
        _totp.Verify(secret, "000000").ShouldBeFalse();
        _totp.Verify(secret, "not-a-code").ShouldBeFalse();
    }

    [Fact]
    public void Builds_an_otpauth_uri()
    {
        var uri = _totp.BuildOtpAuthUri("Nerves", "alice@example.com", "SECRET");

        uri.ShouldStartWith("otpauth://totp/");
        uri.ShouldContain("secret=SECRET");
        uri.ShouldContain("digits=6");
        uri.ShouldContain("period=30");
    }

    [Fact]
    public void Credentials_are_pending_until_activated()
    {
        var credential = AccountCredential.Create(
            Guid.CreateVersion7(), CredentialType.Totp, "ciphertext", "totp-dp", DateTimeOffset.UtcNow).Value;

        credential.IsActive.ShouldBeFalse();

        credential.Activate(DateTimeOffset.UtcNow);

        credential.IsActive.ShouldBeTrue();
        credential.ActivatedAt.ShouldNotBeNull();
    }
}
