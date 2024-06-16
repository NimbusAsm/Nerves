using MongoDB.Driver;

namespace Nerves.Data.MongoDB.Extensions;

public static class MongoClientExtensions
{
    public static void EnsureConnected(this MongoClient? client, string messageWhenNull = "You should init connector first !")
    {
        if (client is null)
            throw new InvalidOperationException(messageWhenNull);
    }
}
