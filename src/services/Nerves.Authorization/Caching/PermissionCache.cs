using System.Collections.Concurrent;

namespace Nerves.Authorization.Caching;

/// <summary>
/// Caches the permission snapshot per (account, legal entity). Entries are
/// invalidated by comparing the stored authorization version with the version
/// read from the database, so a role or group change is visible on the next
/// request without an explicit cache flush.
/// </summary>
public sealed class PermissionCache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private long _hits;
    private long _misses;

    public bool TryGet(string key, long version, out IReadOnlyList<string> permissions)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.Version == version)
        {
            Interlocked.Increment(ref _hits);
            permissions = entry.Permissions;
            return true;
        }

        Interlocked.Increment(ref _misses);
        permissions = [];
        return false;
    }

    public void Set(string key, long version, IReadOnlyList<string> permissions) =>
        _entries[key] = new Entry(version, permissions);

    public CacheStats Snapshot() =>
        new(_entries.Count, Interlocked.Read(ref _hits), Interlocked.Read(ref _misses));

    private sealed record Entry(long Version, IReadOnlyList<string> Permissions);
}

/// <summary>Point-in-time cache counters.</summary>
public sealed record CacheStats(int Entries, long Hits, long Misses)
{
    public double HitRate => Hits + Misses == 0 ? 0 : (double)Hits / (Hits + Misses);
}
