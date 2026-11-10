using Microsoft.Extensions.Configuration;

namespace Nerves.Sharding;

/// <summary>
/// Resolves the connection string for a logical shard. Single-database
/// deployments leave <c>Sharding:ConnectionStrings</c> empty and use
/// <c>ConnectionStrings:Default</c>.
/// </summary>
public sealed class ShardConnectionResolver
{
    public const string SectionName = "Sharding";

    private readonly string[] shards;
    private readonly string? fallback;

    public ShardConnectionResolver(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        fallback = configuration.GetConnectionString("Default");
        var count = configuration.GetValue($"{SectionName}:ShardCount", 1);
        if (count < 1)
        {
            throw new InvalidOperationException("Sharding:ShardCount must be at least 1.");
        }

        ShardCount = count;
        shards = new string[count];

        for (var index = 0; index < count; index++)
        {
            var value = configuration[$"{SectionName}:ConnectionStrings:{index}"];
            if (string.IsNullOrWhiteSpace(value))
            {
                if (count > 1)
                {
                    throw new InvalidOperationException(
                        $"Sharding:ConnectionStrings:{index} is required when ShardCount is {count}.");
                }

                continue;
            }

            shards[index] = value;
        }

        if (count > 1 && string.IsNullOrWhiteSpace(fallback))
        {
            // Multiple shards must be explicit; there is no safe default.
        }
    }

    public int ShardCount { get; }

    public string GetConnectionString(int shard)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shard);
        if (shard >= ShardCount)
        {
            throw new ArgumentOutOfRangeException(nameof(shard), shard, "shard is outside the configured range");
        }

        var value = shards[shard];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        throw new InvalidOperationException(
            $"No connection string for shard {shard}: set Sharding:ConnectionStrings:{shard} or ConnectionStrings:Default.");
    }

    public IReadOnlyList<string> AllConnectionStrings()
    {
        var result = new List<string>(ShardCount);
        for (var index = 0; index < ShardCount; index++)
        {
            result.Add(GetConnectionString(index));
        }

        return result;
    }
}
