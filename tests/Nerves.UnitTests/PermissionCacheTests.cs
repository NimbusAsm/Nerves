using Nerves.Authorization.Caching;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class PermissionCacheTests
{
    [Fact]
    public void Serves_entries_while_the_version_matches()
    {
        var cache = new PermissionCache();
        cache.Set("a|e", 3, ["x:read"]);

        cache.TryGet("a|e", 3, out var permissions).ShouldBeTrue();
        permissions.ShouldBe(["x:read"]);

        cache.TryGet("a|e", 4, out _).ShouldBeFalse();
    }

    [Fact]
    public void Counts_hits_and_misses()
    {
        var cache = new PermissionCache();
        cache.Set("a|e", 1, ["x:read"]);

        cache.TryGet("a|e", 1, out _);
        cache.TryGet("a|e", 1, out _);
        cache.TryGet("a|e", 2, out _);
        cache.TryGet("b|e", 2, out _);

        var stats = cache.Snapshot();
        stats.Hits.ShouldBe(2);
        stats.Misses.ShouldBe(2);
        stats.Entries.ShouldBe(1);
        stats.HitRate.ShouldBe(0.5);
    }

    [Fact]
    public void Replaces_a_stale_entry()
    {
        var cache = new PermissionCache();
        cache.Set("a|e", 1, ["old"]);
        cache.Set("a|e", 2, ["new"]);

        cache.TryGet("a|e", 2, out var permissions).ShouldBeTrue();
        permissions.ShouldBe(["new"]);
    }
}
