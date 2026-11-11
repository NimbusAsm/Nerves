using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nerves.Identity.Sharding;
using Nerves.Sharding;

namespace Nerves.Identity.Persistence;

/// <summary>
/// The identity data plane: one global database (legal entities and the routing
/// index) plus one or more account shards. With sharding disabled everything
/// collapses onto <c>ConnectionStrings:Default</c>, so a single-database
/// deployment behaves exactly as before.
/// </summary>
public sealed class IdentityDataPlane
{
    private readonly DbContextOptions<IdentityDbContext>[] shards;
    private readonly ShardResolver map;

    public IdentityDataPlane(
        ShardConnectionResolver connections,
        IConfiguration configuration,
        IOptions<ShardingOptions> options,
        ShardResolver map)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(map);

        var global = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

        var enabled = options.Value.Enabled;
        var count = enabled ? connections.ShardCount : 1;

        if (enabled)
        {
            // A missing shard connection would silently fall back to the global
            // database and strand half the accounts there, so refuse to start.
            for (var index = 0; index < count; index++)
            {
                if (string.IsNullOrWhiteSpace(configuration[$"Sharding:ConnectionStrings:{index}"]))
                {
                    throw new InvalidOperationException(
                        $"Sharding:ConnectionStrings:{index} is required when Sharding:Enabled is true.");
                }
            }
        }

        this.map = map;

        Global = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(global)
            .Options;

        shards = new DbContextOptions<IdentityDbContext>[count];
        for (var index = 0; index < count; index++)
        {
            var connectionString = enabled
                ? connections.GetConnectionString(index)
                : global;

            shards[index] = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql(connectionString)
                .Options;
        }
    }

    private IdentityDataPlane(string connectionString, ShardResolver map)
    {
        this.map = map;
        Global = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        shards =
        [
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql(connectionString)
                .Options,
        ];
    }

    /// <summary>Global store: legal entities, routing index and verification attempts.</summary>
    public DbContextOptions<IdentityDbContext> Global { get; }

    public bool IsSharded => shards.Length > 1;

    public int ShardCount => shards.Length;

    /// <summary>Physical shard for an account. Always 0 when sharding is off.</summary>
    public int ShardFor(Guid accountId) => map.Resolve(accountId);

    /// <summary>Every configured account shard, in index order.</summary>
    public IReadOnlyList<DbContextOptions<IdentityDbContext>> Shards() => shards;

    public IdentityDbContext CreateGlobal() => new(Global);

    public IdentityDbContext CreateShard(int index) => new(shards[index]);

    public IdentityDbContext CreateShardFor(Guid accountId) => CreateShard(ShardFor(accountId));

    /// <summary>Single-database plane, used by tools that only know one connection string.</summary>
    public static IdentityDataPlane SingleDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var resolver = new ShardResolver(Microsoft.Extensions.Options.Options.Create(new ShardingOptions { ShardCount = 1 }));
        return new IdentityDataPlane(connectionString, resolver);
    }
}
