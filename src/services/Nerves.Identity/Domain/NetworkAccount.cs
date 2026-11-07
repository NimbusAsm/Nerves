using Nerves.Core;

namespace Nerves.Identity.Domain;

/// <summary>
/// A login identity. Many accounts may belong to one legal entity, and they may
/// live in different realms (invariant I3).
/// </summary>
public sealed class NetworkAccount
{
    private NetworkAccount()
    {
        // EF Core materialization.
        Realm = string.Empty;
        Username = string.Empty;
    }

    private NetworkAccount(
        Guid id,
        Guid? legalEntityId,
        string realm,
        string username,
        string? email,
        string? phoneNumber,
        string? displayName,
        AccountType type,
        DateTimeOffset now)
    {
        Id = id;
        LegalEntityId = legalEntityId;
        Realm = realm;
        Username = username;
        Email = email;
        PhoneNumber = phoneNumber;
        DisplayName = displayName;
        Type = type;
        Status = AccountStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    /// <summary>Null only for service accounts (invariant I5).</summary>
    public Guid? LegalEntityId { get; private set; }

    public string Realm { get; private set; }

    public string Username { get; private set; }

    public string? Email { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string? DisplayName { get; private set; }

    public AccountType Type { get; private set; }

    public AccountStatus Status { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DisabledAt { get; private set; }

    public static Result<NetworkAccount> Create(
        Guid? legalEntityId,
        string? realm,
        string? username,
        string? email,
        string? phoneNumber,
        string? displayName,
        AccountType type,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(type))
        {
            return Result.Failure<NetworkAccount>(Reason.Validation("account type is invalid"));
        }

        if (string.IsNullOrWhiteSpace(realm) || realm.Length > 64)
        {
            return Result.Failure<NetworkAccount>(Reason.Validation("realm must be between 1 and 64 characters"));
        }

        if (string.IsNullOrWhiteSpace(username) || username.Length > 64)
        {
            return Result.Failure<NetworkAccount>(Reason.Validation("username must be between 1 and 64 characters"));
        }

        // A person's account must be anchored to a legal entity; a machine's must not.
        if (type == AccountType.Service && legalEntityId is not null)
        {
            return Result.Failure<NetworkAccount>(Reason.Validation("service accounts cannot belong to a legal entity"));
        }

        if (type != AccountType.Service && legalEntityId is null)
        {
            return Result.Failure<NetworkAccount>(Reason.Validation("only service accounts may omit a legal entity"));
        }

        return Result.Success(new NetworkAccount(
            IdGenerator.NewId(now),
            legalEntityId,
            realm.Trim().ToLowerInvariant(),
            username.Trim(),
            Normalize(email),
            Normalize(phoneNumber),
            Normalize(displayName),
            type,
            now));
    }

    public Result Disable(DateTimeOffset now)
    {
        if (Status == AccountStatus.Disabled)
        {
            return Result.Failure(Reason.Conflict("account is already disabled"));
        }

        Status = AccountStatus.Disabled;
        DisabledAt = now;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Lock(DateTimeOffset now)
    {
        if (Status != AccountStatus.Active)
        {
            return Result.Failure(Reason.Conflict("only an active account can be locked"));
        }

        Status = AccountStatus.Locked;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Unlock(DateTimeOffset now)
    {
        if (Status != AccountStatus.Locked)
        {
            return Result.Failure(Reason.Conflict("only a locked account can be unlocked"));
        }

        Status = AccountStatus.Active;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result MarkPrimary()
    {
        if (Type != AccountType.Personal)
        {
            return Result.Failure(Reason.Conflict("only personal accounts can be marked primary"));
        }

        IsPrimary = true;
        return Result.Success();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
