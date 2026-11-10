namespace Nerves.Idp.Federation;

/// <summary>Mutable view of the last directory sync run, exposed to operators.</summary>
public sealed class DirectorySyncState
{
    private readonly Lock _gate = new();
    private DirectorySyncSnapshot _snapshot = DirectorySyncSnapshot.Never;

    public DirectorySyncSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    public void Started(DateTimeOffset startedAt)
    {
        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                LastStartedAt = startedAt,
                Running = true,
            };
        }
    }

    public void Completed(DirectorySyncSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_gate)
        {
            _snapshot = snapshot;
        }
    }
}

/// <summary>Result of the most recent run. Counts are cumulative per run.</summary>
public sealed record DirectorySyncSnapshot(
    DateTimeOffset? LastStartedAt,
    DateTimeOffset? LastCompletedAt,
    TimeSpan? Duration,
    bool Running,
    string? LastError,
    int Scanned,
    int Created,
    int Updated,
    int Unchanged,
    int Skipped,
    int Failed)
{
    public static DirectorySyncSnapshot Never { get; } = new(
        null, null, null, false, null, 0, 0, 0, 0, 0, 0);

    public static DirectorySyncSnapshot FailedRun(DateTimeOffset startedAt, string error) =>
        new(startedAt, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow - startedAt, false, error, 0, 0, 0, 0, 0, 0);
}
