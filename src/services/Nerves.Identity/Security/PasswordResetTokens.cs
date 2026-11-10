using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Domain;
using Nerves.Identity.Persistence;

namespace Nerves.Identity.Security;

/// <summary>Issues, hashes and redeems single-use password reset tokens.</summary>
public sealed class PasswordResetTokens(IdentityDbContext db)
{
    /// <summary>
    /// Issues a token for the account. Any token issued earlier for the same
    /// account stays valid until it expires; confirmation spends one token only,
    /// and a successful reset revokes every session anyway.
    /// </summary>
    public async Task<string> IssueAsync(
        Guid accountId,
        DateTimeOffset now,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var token = PasswordResetToken.Issue(accountId, Hash(raw), now, lifetime);

        if (token.IsFailure)
        {
            throw new InvalidOperationException(token.Reason.Message);
        }

        db.PasswordResetTokens.Add(token.Value);
        await db.SaveChangesAsync(cancellationToken);
        return raw;
    }

    /// <summary>Returns the matching token when it is unused and unexpired.</summary>
    public async Task<PasswordResetToken?> FindUsableAsync(
        string? raw,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var hash = Hash(raw);
        var token = await db.PasswordResetTokens
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        return token is null || !token.IsUsable(now) ? null : token;
    }

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
