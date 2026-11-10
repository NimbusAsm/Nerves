using Nerves.Core;

namespace Nerves.Identity.Domain;

/// <summary>
/// A single-use token that authorises one password reset. Only the SHA-256 hash
/// of the token is stored, so a database leak does not hand out resets.
/// </summary>
public sealed class PasswordResetToken
{
    private PasswordResetToken()
    {
        TokenHash = string.Empty;
    }

    private PasswordResetToken(Guid id, Guid accountId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        Id = id;
        AccountId = accountId;
        TokenHash = tokenHash;
        CreatedAt = now;
        ExpiresAt = now.Add(lifetime);
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public static Result<PasswordResetToken> Issue(
        Guid accountId,
        string? tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        if (accountId == Guid.Empty)
        {
            return Result.Failure<PasswordResetToken>(Reason.Validation("account id is required"));
        }

        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length != 64)
        {
            return Result.Failure<PasswordResetToken>(Reason.Validation("token hash must be a 64 character hex digest"));
        }

        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromHours(24))
        {
            return Result.Failure<PasswordResetToken>(Reason.Validation("token lifetime must be between 0 and 24 hours"));
        }

        return Result.Success(new PasswordResetToken(
            IdGenerator.NewId(now), accountId, tokenHash, now, lifetime));
    }

    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && now < ExpiresAt;

    /// <summary>Marks the token as spent. A second confirmation cannot succeed.</summary>
    public Result Consume(DateTimeOffset now)
    {
        if (ConsumedAt is not null)
        {
            return Result.Failure(Reason.Conflict("password reset token has already been used"));
        }

        if (now >= ExpiresAt)
        {
            return Result.Failure(Reason.Conflict("password reset token has expired"));
        }

        ConsumedAt = now;
        return Result.Success();
    }
}
