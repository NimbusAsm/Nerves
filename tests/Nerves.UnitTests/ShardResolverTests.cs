using Nerves.Identity.Sharding;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class ShardResolverTests
{
    [Fact]
    public void A_single_shard_always_resolves_to_zero()
    {
        var resolver = new ShardResolver(
            Microsoft.Extensions.Options.Options.Create(new ShardingOptions { ShardCount = 1 }));

        resolver.Resolve(Guid.CreateVersion7()).ShouldBe(0);
    }

    [Fact]
    public void The_mapping_is_stable_and_within_range()
    {
        var resolver = new ShardResolver(
            Microsoft.Extensions.Options.Options.Create(new ShardingOptions { ShardCount = 16 }));

        var id = Guid.CreateVersion7();
        var first = resolver.Resolve(id);

        resolver.Resolve(id).ShouldBe(first);
        first.ShouldBeInRange(0, 15);
    }

    [Fact]
    public void Identifiers_are_spread_across_shards()
    {
        var resolver = new ShardResolver(
            Microsoft.Extensions.Options.Options.Create(new ShardingOptions { ShardCount = 8 }));

        var shards = Enumerable.Range(0, 200)
            .Select(_ => resolver.Resolve(Guid.CreateVersion7()))
            .Distinct()
            .Count();

        shards.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void A_zero_count_is_treated_as_one()
    {
        var resolver = new ShardResolver(
            Microsoft.Extensions.Options.Options.Create(new ShardingOptions { ShardCount = 0 }));

        resolver.ShardCount.ShouldBe(1);
        resolver.Resolve(Guid.CreateVersion7()).ShouldBe(0);
    }
}
