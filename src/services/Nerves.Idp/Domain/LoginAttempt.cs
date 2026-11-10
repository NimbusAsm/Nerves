namespace Nerves.Idp.Domain;

/// <summary>
/// Failed-login counters persisted so every IdP replica sees the same lockout
/// state. Keys are SHA-256 hashes of the normalized identifier.
/// </summary>
public sealed class LoginAttempt
{
    private LoginAttempt() => IdentifierHash = string.Empty;

    private LoginAttempt(Guid id, string identifierHash)
    {
        Id = id;
        IdentifierHash = identifierHash;
    }

    public Guid Id { get; private set; }

    public string IdentifierHash { get; private set; }

    public int Failures { get; private set; }

    public DateTimeOffset FirstFailureAt { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public static LoginAttempt Create(string identifierHash, DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), identifierHash);

    public bool IsLocked(DateTimeOffset now) => LockedUntil is not null && LockedUntil > now;

    /// <summary>Records a failure and locks once the threshold is reached.</summary>
    public void RegisterFailure(DateTimeOffset now, int threshold, TimeSpan window, TimeSpan lockout)
    {
        if (FirstFailureAt == default || now - FirstFailureAt > window)
        {
            FirstFailureAt = now;
            Failures = 0;
        }

        Failures++;

        if (Failures >= threshold)
        {
            LockedUntil = now.Add(lockout);
            Failures = 0;
            FirstFailureAt = now;
        }
    }

    public void Reset()
    {
        Failures = 0;
        LockedUntil = null;
        FirstFailureAt = default;
    }
}
