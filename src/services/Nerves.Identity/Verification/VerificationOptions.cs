using Nerves.Core;

namespace Nerves.Identity.Verification;

/// <summary>Verification channel selection (<c>Verification:*</c>).</summary>
public sealed class VerificationOptions
{
    public const string SectionName = "Verification";

    /// <summary>Either <c>manual</c> (default) or <c>http</c>.</summary>
    public string Provider { get; set; } = "manual";

    /// <summary>
    /// Public base URL the provider must call back. The identity service appends
    /// <c>/api/v1/legal-entities/verification/callback</c>.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>How long an attempt may stay pending before it is reported as expired.</summary>
    public int AttemptLifetimeMinutes { get; set; } = 1440;

    public Result Validate()
    {
        if (Provider is not ("manual" or "http"))
        {
            return Result.Failure(Reason.Validation("Verification:Provider must be 'manual' or 'http'"));
        }

        if (Provider == "http" && string.IsNullOrWhiteSpace(PublicBaseUrl))
        {
            return Result.Failure(Reason.Validation("Verification:PublicBaseUrl is required when provider is 'http'"));
        }

        if (AttemptLifetimeMinutes is < 5 or > 43200)
        {
            return Result.Failure(Reason.Validation("Verification:AttemptLifetimeMinutes must be between 5 and 43200"));
        }

        return Result.Success();
    }
}
