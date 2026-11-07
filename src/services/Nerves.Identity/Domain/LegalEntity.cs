using Nerves.Core;

namespace Nerves.Identity.Domain;

/// <summary>
/// A legal entity (natural person, company, government body) that owns one
/// verified identity and any number of network accounts.
/// </summary>
public sealed class LegalEntity
{
    private LegalEntity()
    {
        // EF Core materialization.
        DisplayName = string.Empty;
        RealName = null!;
    }

    private LegalEntity(Guid id, LegalEntityType type, string displayName, RealNameId realName, DateTimeOffset now)
    {
        Id = id;
        Type = type;
        DisplayName = displayName;
        RealName = realName;
        VerificationState = VerificationState.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public LegalEntityType Type { get; private set; }

    public string DisplayName { get; private set; }

    /// <summary>One legal entity has exactly one real-name identity (invariant I1).</summary>
    public RealNameId RealName { get; private set; }

    public VerificationState VerificationState { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    public string? ProviderReference { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<LegalEntity> Create(
        LegalEntityType type,
        string? displayName,
        RealNameId realName,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(realName);

        if (!Enum.IsDefined(type))
        {
            return Result.Failure<LegalEntity>(Reason.Validation("legal entity type is invalid"));
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200)
        {
            return Result.Failure<LegalEntity>(Reason.Validation("display name must be between 1 and 200 characters"));
        }

        return Result.Success(new LegalEntity(IdGenerator.NewId(now), type, displayName.Trim(), realName, now));
    }

    public Result MarkVerified(string? providerReference, DateTimeOffset now)
    {
        if (VerificationState == VerificationState.Verified)
        {
            return Result.Failure(Reason.Conflict("legal entity is already verified"));
        }

        if (VerificationState == VerificationState.Expired)
        {
            return Result.Failure(Reason.Conflict("cannot verify an expired legal entity; create a new one"));
        }

        VerificationState = VerificationState.Verified;
        VerifiedAt = now;
        ProviderReference = providerReference;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result MarkRejected(string? providerReference, DateTimeOffset now)
    {
        if (VerificationState != VerificationState.Pending)
        {
            return Result.Failure(Reason.Conflict("only a pending legal entity can be rejected"));
        }

        VerificationState = VerificationState.Rejected;
        ProviderReference = providerReference;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Expire(DateTimeOffset now)
    {
        if (VerificationState != VerificationState.Verified)
        {
            return Result.Failure(Reason.Conflict("only a verified legal entity can expire"));
        }

        VerificationState = VerificationState.Expired;
        UpdatedAt = now;
        return Result.Success();
    }
}
