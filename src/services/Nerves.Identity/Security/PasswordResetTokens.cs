using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Domain;
using Nerves.Identity.Persistence;

namespace Nerves.Identity.Security;

/// <summary>Issues, hashes and redeems single-use password reset tokens.</summary>
/// <remarks>
/// Tokens are account scoped and are stored on the account's shard, next to the
/// credential they can replace.
/// </remarks>
public sealed class PasswordResetTokens(IdentityStore store)
{
    /// <summary>Issues a raw token for the account on its shard.</summary>
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

        await using var db = store.AccountShard(accountId);
        db.PasswordResetTokens.Add(token.Value);
        await db.SaveChangesAsync(cancellationToken);
        return raw;
    }

    /// <summary>
    /// Finds a usable token by its raw value. Token ids do not encode a shard,
    /// so the lookup fans out and stops at the owning shard.
    /// </summary>
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

        foreach (var options in store.Plane.Shards())
        {
            await using var db = new IdentityDbContext(options);
            var token = await db.PasswordResetTokens
                .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

            if (token is not null)
            {
                return token.IsUsable(now) ? token : null;
            }
        }

        return null;
    }

    /// <summary>Persists a spent token back to the shard that owns it.</summary>
    public async Task ConsumeAsync(PasswordResetToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        await using var db = store.AccountShard(token.AccountId);
        db.PasswordResetTokens.Update(token);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
