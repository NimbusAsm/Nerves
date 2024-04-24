using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nerves.Data.MongoDB.Extensions;

namespace Nerves.Data.MongoDB;

public class DataBaseConnector
{
    private readonly MongoClient? mongoClient;

    public string ConnectionString { get; init; }

    public string DataBaseName { get; init; }

    public bool IsConnected => mongoClient is not null;

    public DataBaseConnector(string connectStr, string dbName)
    {
        ConnectionString = connectStr;

        DataBaseName = dbName;

        mongoClient = new(ConnectionString);

        Console.WriteLine($"@Init: {nameof(DataBaseConnector)}, Connected: {IsConnected}");
    }

    public IMongoCollection<T> GetCollection<T>(string colName)
    {
        mongoClient.EnsureConnected();

        return mongoClient!.GetDatabase(DataBaseName).GetCollection<T>(colName);
    }

    public IMongoQueryable<T> QueryCollection<T>(string colName)
    {
        mongoClient.EnsureConnected();

        var collection = GetCollection<T>(colName);

        return collection.AsQueryable();
    }
}
