namespace Nerves.Identity.Verification;

/// <summary>What happened when a verification was started with a provider.</summary>
public enum VerificationProviderOutcome
{
    AwaitingManualReview = 1,
    Processing = 2,
    Verified = 3,
    Rejected = 4,
    Failed = 5,
}

/// <summary>Input a provider needs to start a verification.</summary>
/// <remarks>
/// The raw document number never leaves the identity service: providers receive
/// the same HMAC digest that is stored on the legal entity.
/// </remarks>
public sealed record VerificationProviderRequest(
    Guid LegalEntityId,
    string LegalEntityType,
    string DocumentType,
    string DocumentHash,
    string Country,
    string DisplayName,
    string CallbackUrl,
    DateTimeOffset RequestedAt);

/// <summary>Outcome reported by a provider when a verification starts.</summary>
public sealed record VerificationProviderResponse(
    VerificationProviderOutcome Outcome,
    string? Reference,
    string? Message);

/// <summary>
/// A real-name verification channel. Implementations must be stateless: all
/// state lives in <see cref="Domain.VerificationAttempt"/> so a restart never
/// loses an in-flight verification.
/// </summary>
public interface IVerificationProvider
{
    /// <summary>Stable configuration name, for example <c>manual</c> or <c>http</c>.</summary>
    string Name { get; }

    Task<VerificationProviderResponse> StartAsync(
        VerificationProviderRequest request,
        CancellationToken cancellationToken);
}
