using System.Collections.Concurrent;

namespace Nerves.Idp.Security;

/// <summary>
/// In-memory login throttling: five failures within the window lock the
/// identifier for the lockout duration. A distributed implementation backed by
/// Redis replaces this when the service runs behind multiple replicas.
/// </summary>
public sealed class LoginThrottle
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, Attempt> _attempts = new(StringComparer.Ordinal);

    public bool IsLocked(string identifier, DateTimeOffset now) =>
        _attempts.TryGetValue(identifier, out var attempt) && attempt.LockedUntil > now;

    public void RecordFailure(string identifier, DateTimeOffset now)
    {
        var attempt = _attempts.GetOrAdd(identifier, _ => new Attempt());
        lock (attempt)
        {
            if (attempt.LockedUntil > now)
            {
                return;
            }

            if (now - attempt.FirstFailureAt > Window)
            {
                attempt.FirstFailureAt = now;
                attempt.Count = 0;
            }

            attempt.Count++;

            if (attempt.Count >= MaxFailures)
            {
                attempt.LockedUntil = now.Add(Lockout);
                attempt.Count = 0;
                attempt.FirstFailureAt = now;
            }
        }
    }

    public void Reset(string identifier) => _attempts.TryRemove(identifier, out _);

    private sealed class Attempt
    {
        public int Count { get; set; }

        public DateTimeOffset FirstFailureAt { get; set; }

        public DateTimeOffset LockedUntil { get; set; }
    }
}
