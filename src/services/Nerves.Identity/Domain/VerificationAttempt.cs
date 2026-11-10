using Nerves.Core;

namespace Nerves.Identity.Domain;

/// <summary>
/// One attempt to verify a legal entity through a provider. The attempt keeps
/// the external reference and the asynchronous outcome; the entity itself keeps
/// the authoritative <see cref="VerificationState"/>.
/// </summary>
public sealed class VerificationAttempt
{
    private VerificationAttempt()
    {
        Provider = string.Empty;
    }

    private VerificationAttempt(
        Guid id,
        Guid legalEntityId,
        string provider,
        string? reference,
        DateTimeOffset now)
    {
        Id = id;
        LegalEntityId = legalEntityId;
        Provider = provider;
        Reference = reference;
        Status = VerificationAttemptStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid LegalEntityId { get; private set; }

    /// <summary>Name of the provider that owns this attempt (<c>manual</c>, <c>http</c>).</summary>
    public string Provider { get; private set; }

    /// <summary>Opaque provider-side identifier used to correlate a callback.</summary>
    public string? Reference { get; private set; }

    public VerificationAttemptStatus Status { get; private set; }

    public string? Message { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsPending => Status == VerificationAttemptStatus.Pending;

    public static Result<VerificationAttempt> Create(
        Guid legalEntityId,
        string? provider,
        string? reference,
        DateTimeOffset now)
    {
        if (legalEntityId == Guid.Empty)
        {
            return Result.Failure<VerificationAttempt>(Reason.Validation("legal entity id is required"));
        }

        if (string.IsNullOrWhiteSpace(provider) || provider.Length > 32)
        {
            return Result.Failure<VerificationAttempt>(Reason.Validation("provider must be between 1 and 32 characters"));
        }

        if (reference is { Length: > 200 })
        {
            return Result.Failure<VerificationAttempt>(Reason.Validation("provider reference must not exceed 200 characters"));
        }

        return Result.Success(new VerificationAttempt(
            IdGenerator.NewId(now),
            legalEntityId,
            provider.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            now));
    }

    /// <summary>
    /// Records the provider outcome. Only a pending attempt can complete, so a
    /// replayed callback cannot flip a decision that was already applied.
    /// </summary>
    public Result Complete(VerificationAttemptStatus status, string? message, DateTimeOffset now)
    {
        if (!Enum.IsDefined(status) || status == VerificationAttemptStatus.Pending)
        {
            return Result.Failure(Reason.Validation("verification attempt status is invalid"));
        }

        if (!IsPending)
        {
            return Status == status
                ? Result.Success()
                : Result.Failure(Reason.Conflict("verification attempt is already completed with a different outcome"));
        }

        if (message is { Length: > 500 })
        {
            return Result.Failure(Reason.Validation("verification message must not exceed 500 characters"));
        }

        Status = status;
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        CompletedAt = now;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Attaches a provider reference discovered after the attempt started.</summary>
    public Result AttachReference(string? reference, DateTimeOffset now)
    {
        if (reference is { Length: > 200 })
        {
            return Result.Failure(Reason.Validation("provider reference must not exceed 200 characters"));
        }

        Reference = string.IsNullOrWhiteSpace(reference) ? Reference : reference.Trim();
        UpdatedAt = now;
        return Result.Success();
    }
}
