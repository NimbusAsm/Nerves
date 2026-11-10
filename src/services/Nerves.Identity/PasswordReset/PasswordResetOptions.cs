using Nerves.Core;

namespace Nerves.Identity.PasswordReset;

/// <summary>Password reset settings (<c>PasswordReset:*</c>).</summary>
public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>Realm used when the caller does not supply one.</summary>
    public string DefaultRealm { get; set; } = "nerves";

    /// <summary>
    /// Base URL of the page that accepts the token. When empty the IdP fills in
    /// the address it is reached on.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Lifetime of a reset token, in minutes.</summary>
    public int LifetimeMinutes { get; set; } = 15;

    public Result Validate()
    {
        if (string.IsNullOrWhiteSpace(DefaultRealm) || DefaultRealm.Length > 64)
        {
            return Result.Failure(Reason.Validation("PasswordReset:DefaultRealm must be between 1 and 64 characters"));
        }

        if (LifetimeMinutes is < 5 or > 120)
        {
            return Result.Failure(Reason.Validation("PasswordReset:LifetimeMinutes must be between 5 and 120"));
        }

        return Result.Success();
    }
}
