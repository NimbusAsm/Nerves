using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Nerves.Identity.Sharding;

/// <summary>Sharding configuration. The data plane stays single-node for now.</summary>
public sealed class ShardingOptions
{
    public const string SectionName = "Sharding";

    /// <summary>Number of logical shards. Must be stable for the life of the data.</summary>
    public int ShardCount { get; set; } = 1;
}

/// <summary>
/// Maps an account to its logical shard. The mapping is a pure function of the
/// account id so every service can compute it without a lookup; the routing
/// index additionally records it for operators and for migrations.
/// </summary>
public sealed class ShardResolver(IOptions<ShardingOptions> options)
{
    public int ShardCount => Math.Max(options.Value.ShardCount, 1);

    public int Resolve(Guid accountId)
    {
        if (ShardCount == 1)
        {
            return 0;
        }

        var bytes = Encoding.UTF8.GetBytes(accountId.ToString("N"));
        var hash = SHA256.HashData(bytes);
        var value = BitConverter.ToUInt32(hash, 0);
        return (int)(value % (uint)ShardCount);
    }
}
