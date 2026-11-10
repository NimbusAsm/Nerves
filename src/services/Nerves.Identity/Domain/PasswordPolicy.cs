using Nerves.Core;

namespace Nerves.Identity.Domain;

/// <summary>Minimum strength rules for a network account password.</summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 12;

    public static Result Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
        {
            return Result.Failure(Reason.Validation($"password must be at least {MinimumLength} characters"));
        }

        if (password.Length > 256)
        {
            return Result.Failure(Reason.Validation("password must be at most 256 characters"));
        }

        return Result.Success();
    }
}
