using OtpNet;

namespace Nerves.Identity.Security;

/// <summary>Generates TOTP secrets and verifies six-digit codes.</summary>
public sealed class TotpService
{
    /// <summary>Verification window of ±1 step tolerates clock drift.</summary>
    private static readonly VerificationWindow Window = new(1, 1);

    public string GenerateSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    public string BuildOtpAuthUri(string issuer, string accountName, string secret) =>
        "otpauth://totp/"
        + Uri.EscapeDataString(issuer)
        + ":"
        + Uri.EscapeDataString(accountName)
        + "?secret="
        + secret
        + "&issuer="
        + Uri.EscapeDataString(issuer)
        + "&algorithm=SHA1&digits=6&period=30";

    public bool Verify(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        try
        {
            var totp = new Totp(Base32Encoding.ToBytes(secret));
            return totp.VerifyTotp(code, out _, Window);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
