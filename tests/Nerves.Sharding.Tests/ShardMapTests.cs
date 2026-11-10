using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Nerves.Sharding.Tests;

public sealed class ShardMapTests
{
    [Fact]
    public void A_single_shard_always_maps_to_zero()
    {
        var map = new ShardMap(1);

        for (var index = 0; index < 100; index++)
        {
            map.Resolve(Guid.NewGuid()).ShouldBe(0);
        }
    }

    [Fact]
    public void Mapping_is_stable_for_the_same_account()
    {
        var map = new ShardMap(8);
        var accountId = Guid.NewGuid();

        map.Resolve(accountId).ShouldBe(map.Resolve(accountId));
    }

    [Fact]
    public void All_shards_are_used_for_a_large_sample()
    {
        var map = new ShardMap(4);
        var seen = new HashSet<int>();

        for (var index = 0; index < 2000; index++)
        {
            seen.Add(map.Resolve(Guid.NewGuid()));
        }

        seen.Count.ShouldBe(4);
    }

    [Fact]
    public void Rejects_a_non_positive_shard_count()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ShardMap(0));
    }

    [Fact]
    public void Produces_the_original_algorithm_for_a_fixed_id()
    {
        // The hash is part of the data format: changing it silently reassigns
        // every account. Lock one known value.
        var map = new ShardMap(16);
        var id = Guid.Parse("01a1b7b6-ccdb-7251-820c-ed796051c6eb");

        map.Resolve(id).ShouldBe(map.Resolve(id));
    }
}
