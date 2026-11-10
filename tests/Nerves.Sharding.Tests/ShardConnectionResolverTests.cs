using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Nerves.Sharding.Tests;

public sealed class ShardConnectionResolverTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void A_single_database_falls_back_to_the_default_connection()
    {
        var resolver = new ShardConnectionResolver(Config(new()
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Database=nerves",
        }));

        resolver.ShardCount.ShouldBe(1);
        resolver.GetConnectionString(0).ShouldBe("Host=localhost;Database=nerves");
    }

    [Fact]
    public void Multiple_shards_require_every_connection_string()
    {
        var values = new Dictionary<string, string?>
        {
            ["Sharding:ShardCount"] = "2",
            ["Sharding:ConnectionStrings:0"] = "Host=shard0",
        };

        Should.Throw<InvalidOperationException>(() => new ShardConnectionResolver(Config(values)));
    }

    [Fact]
    public void Resolves_each_shard_connection()
    {
        var resolver = new ShardConnectionResolver(Config(new()
        {
            ["Sharding:ShardCount"] = "2",
            ["Sharding:ConnectionStrings:0"] = "Host=shard0",
            ["Sharding:ConnectionStrings:1"] = "Host=shard1",
        }));

        resolver.GetConnectionString(0).ShouldBe("Host=shard0");
        resolver.GetConnectionString(1).ShouldBe("Host=shard1");
        resolver.AllConnectionStrings().Count.ShouldBe(2);
    }

    [Fact]
    public void Rejects_a_shard_outside_the_range()
    {
        var resolver = new ShardConnectionResolver(Config(new()
        {
            ["Sharding:ShardCount"] = "1",
            ["ConnectionStrings:Default"] = "Host=localhost",
        }));

        Should.Throw<ArgumentOutOfRangeException>(() => resolver.GetConnectionString(1));
    }

    [Fact]
    public async Task The_migration_runner_reports_each_shard()
    {
        var resolver = new ShardConnectionResolver(Config(new()
        {
            ["Sharding:ShardCount"] = "2",
            ["Sharding:ConnectionStrings:0"] = "Host=shard0",
            ["Sharding:ConnectionStrings:1"] = "Host=shard1",
        }));

        var runner = new ShardMigrationRunner(resolver);
        var applied = new List<int>();

        var results = await runner.RunAsync(shard =>
        {
            applied.Add(shard);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        applied.ShouldBe([0, 1]);
        results.Count.ShouldBe(2);
        results.ShouldAllBe(result => result.Succeeded);
    }

    [Fact]
    public async Task A_failing_shard_does_not_stop_the_others()
    {
        var resolver = new ShardConnectionResolver(Config(new()
        {
            ["Sharding:ShardCount"] = "2",
            ["Sharding:ConnectionStrings:0"] = "Host=shard0",
            ["Sharding:ConnectionStrings:1"] = "Host=shard1",
        }));

        var runner = new ShardMigrationRunner(resolver);

        var results = await runner.RunAsync(shard =>
        {
            if (shard == 0)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        results[0].Succeeded.ShouldBeFalse();
        results[0].Error.ShouldNotBeNull().ShouldContain("boom");
        results[1].Succeeded.ShouldBeTrue();
    }
}
