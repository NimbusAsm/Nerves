using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Nerves.Identity.Security;

/// <summary>
/// HMAC-SHA256 over <c>type|realm|normalized value</c>. Uses a server-side key
/// so that hashes cannot be reversed with a rainbow table.
/// </summary>
public sealed class HmacIdentifierHasher : IIdentifierHasher
{
    private readonly byte[] _key;

    public HmacIdentifierHasher(IOptions<IdentitySecurityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configured = options.Value.IdentifierHashKey;
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "Security:IdentifierHashKey is not configured. Refusing to start with an unkeyed identifier hash.");
        }

        _key = Convert.FromBase64String(configured);
        if (_key.Length < 32)
        {
            throw new InvalidOperationException("Security:IdentifierHashKey must be at least 32 bytes.");
        }
    }

    public string Hash(string identifierType, string realm, string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifierType);
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        var normalized = identifierType
            + "|"
            + realm.ToLowerInvariant()
            + "|"
            + identifier.Trim().ToLowerInvariant();

        var digest = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
