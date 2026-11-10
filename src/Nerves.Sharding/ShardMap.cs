using System.Security.Cryptography;
using System.Text;

namespace Nerves.Sharding;

/// <summary>
/// Pure function from an account id to a logical shard. Stable for the life of
/// the data: changing the count requires a routing-index reconciliation pass.
/// </summary>
public sealed class ShardMap
{
    public ShardMap(int shardCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(shardCount, 1);
        ShardCount = shardCount;
    }

    public int ShardCount { get; }

    public int Resolve(Guid accountId)
    {
        if (ShardCount == 1)
        {
            return 0;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(accountId.ToString("N")));
        return (int)(BitConverter.ToUInt32(hash, 0) % (uint)ShardCount);
    }
}
