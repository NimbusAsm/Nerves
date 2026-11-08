using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Nerves.Identity.Security;

/// <summary>
/// Generates single-use recovery codes and hashes them for storage. Plaintext
/// codes are returned once and never persisted.
/// </summary>
public sealed class RecoveryCodes
{
    public const int Count = 10;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public IReadOnlyList<string> Generate()
    {
        var codes = new List<string>(Count);

        for (var i = 0; i < Count; i++)
        {
            var bytes = RandomNumberGenerator.GetBytes(8);
            var builder = new StringBuilder(9);

            for (var index = 0; index < bytes.Length; index++)
            {
                if (index == 4)
                {
                    builder.Append('-');
                }

                builder.Append(Alphabet[bytes[index] % Alphabet.Length]);
            }

            codes.Add(builder.ToString());
        }

        return codes;
    }

    public static string Normalize(string code) =>
        code.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpper(CultureInfo.InvariantCulture);

    public static string HashCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code)))).ToLowerInvariant();
}
