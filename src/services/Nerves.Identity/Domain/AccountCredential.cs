using Nerves.Core;

namespace Nerves.Identity.Domain;

/// <summary>Authentication factor bound to a network account.</summary>
public enum CredentialType
{
    Password = 1,
    Totp = 2,
    RecoveryCode = 3,
    Passkey = 4,
}

/// <summary>
/// A credential record. Secrets are stored only in their hashed or encrypted
/// form; the algorithm travels with the secret so parameters can be upgraded
/// without a flag day.
/// </summary>
public sealed class AccountCredential
{
    private AccountCredential()
    {
        Secret = string.Empty;
        Algorithm = string.Empty;
    }

    private AccountCredential(Guid id, Guid accountId, CredentialType type, string secret, string algorithm, DateTimeOffset now)
    {
        Id = id;
        AccountId = accountId;
        Type = type;
        Secret = secret;
        Algorithm = algorithm;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    public CredentialType Type { get; private set; }

    /// <summary>PHC-style hash for passwords, ciphertext for TOTP secrets.</summary>
    public string Secret { get; private set; }

    public string Algorithm { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public static Result<AccountCredential> Create(Guid accountId, CredentialType type, string? secret, string? algorithm, DateTimeOffset now)
    {
        if (accountId == Guid.Empty)
        {
            return Result.Failure<AccountCredential>(Reason.Validation("account id is required"));
        }

        if (!Enum.IsDefined(type))
        {
            return Result.Failure<AccountCredential>(Reason.Validation("credential type is invalid"));
        }

        if (string.IsNullOrWhiteSpace(secret) || secret.Length > 1000)
        {
            return Result.Failure<AccountCredential>(Reason.Validation("credential secret is required"));
        }

        if (string.IsNullOrWhiteSpace(algorithm) || algorithm.Length > 64)
        {
            return Result.Failure<AccountCredential>(Reason.Validation("credential algorithm is required"));
        }

        return Result.Success(new AccountCredential(IdGenerator.NewId(now), accountId, type, secret, algorithm, now));
    }

    public void MarkUsed(DateTimeOffset now) => LastUsedAt = now;

    public void ReplaceSecret(string secret, string algorithm, DateTimeOffset now)
    {
        Secret = secret;
        Algorithm = algorithm;
        UpdatedAt = now;
    }
}
