using System.Globalization;
using System.Security.Cryptography;

namespace Nerves.Identity.Security;

/// <summary>
/// PBKDF2-HMAC-SHA512 with 100,000 iterations, the framework-maintained
/// baseline recorded in PLAN §4.2. Hashes are self-describing
/// (<c>pbkdf2-sha512$iterations$salt$key</c>) so parameters can be raised later
/// while old hashes keep verifying.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const string Algorithm = "pbkdf2-sha512";
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, KeySize);

        return Algorithm
            + "$" + Iterations
            + "$" + Convert.ToBase64String(salt)
            + "$" + Convert.ToBase64String(key);
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var parts = hash.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], Algorithm, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], CultureInfo.InvariantCulture, out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
