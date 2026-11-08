using Microsoft.AspNetCore.DataProtection;

namespace Nerves.Identity.Security;

/// <summary>
/// Encrypts TOTP secrets at rest with the ASP.NET Core data protection key
/// ring. A database leak alone does not expose authenticator seeds.
/// </summary>
public sealed class TotpSecretProtector
{
    private readonly IDataProtector _protector;

    public TotpSecretProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector("nerves.totp.v1");
    }

    public string Protect(string secret) => _protector.Protect(secret);

    public string? TryUnprotect(string payload)
    {
        try
        {
            return _protector.Unprotect(payload);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
