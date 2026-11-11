using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Nerves.Identity.Sharding;

/// <summary>Sharding configuration.</summary>
public sealed class ShardingOptions
{
    public const string SectionName = "Sharding";

    /// <summary>
    /// When false the identity service keeps everything in ConnectionStrings:Default.
    /// When true ConnectionStrings:Default is the global database and
    /// Sharding:ConnectionStrings:0..N are the account shards.
    /// </summary>
    public bool Enabled { get; set; }

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
