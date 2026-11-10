using Nerves.Identity.Domain;

namespace Nerves.Identity.Grpc;

/// <summary>What an upsert request should do to the identity store.</summary>
public enum DirectoryAccountAction
{
    Create = 1,
    Update = 2,
    Unchanged = 3,
    EmailConflict = 4,
}

/// <summary>Decision returned by <see cref="DirectoryAccountPlanner"/>.</summary>
public sealed record DirectoryAccountPlan(
    DirectoryAccountAction Action,
    string? Email,
    string? DisplayName,
    NetworkAccount? Account)
{
    public static DirectoryAccountPlan Create(string? email, string? displayName) =>
        new(DirectoryAccountAction.Create, email, displayName, null);

    public static DirectoryAccountPlan EmailConflict() =>
        new(DirectoryAccountAction.EmailConflict, null, null, null);
}

/// <summary>
/// Pure idempotency rules for directory provisioning: match by realm+username
/// first, then by email; never touch credentials or the username. Keeping the
/// decision separate from EF makes the rules directly testable.
/// </summary>
public static class DirectoryAccountPlanner
{
    public static DirectoryAccountPlan Plan(
        NetworkAccount? byUsername,
        NetworkAccount? byEmail,
        string? email,
        string? displayName,
        bool emailTakenByAnotherAccount)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedDisplayName = Normalize(displayName);

        if (emailTakenByAnotherAccount)
        {
            return DirectoryAccountPlan.EmailConflict();
        }

        var existing = byUsername ?? byEmail;

        if (existing is null)
        {
            return DirectoryAccountPlan.Create(normalizedEmail, normalizedDisplayName);
        }

        // Null means the directory does not manage this attribute; it can
        // neither change the value nor count as a change.
        var emailChanged = normalizedEmail is not null
            && !string.Equals(existing.Email, normalizedEmail, StringComparison.Ordinal);
        var displayNameChanged = normalizedDisplayName is not null
            && !string.Equals(existing.DisplayName, normalizedDisplayName, StringComparison.Ordinal);

        return emailChanged || displayNameChanged
            ? new DirectoryAccountPlan(DirectoryAccountAction.Update, normalizedEmail, normalizedDisplayName, existing)
            : new DirectoryAccountPlan(DirectoryAccountAction.Unchanged, normalizedEmail, normalizedDisplayName, existing);
    }

    public static string? NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
