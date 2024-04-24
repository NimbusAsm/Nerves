using MongoDB.Driver;
using Nerves.Shared.Options.DataBaseOptions;
using Nerves.Shared.Models.User;

namespace Nerves.Data.MongoDB;

public class UserManager
{
    public UserManager(DataBaseConnector connector)
    {
        this.connector = connector;

        Console.WriteLine($"@Init: {nameof(UserManager)}");
    }

    private readonly DataBaseConnector connector;

    public async Task<User?> GetUserByNameAsync(string id)
    {
        var users = connector.GetCollection<User>("Users");
        var usersQueried = (await users.FindAsync(u => u.Id!.Equals(id))).ToList();
        return usersQueried.Count == 0 ? null : usersQueried.First();
    }

    public async void InsertUserAsync(User user, InsertUserOption? option = default)
    {
        option ??= new();

        var users = connector.GetCollection<User>("Users");

        switch (option.ActionWhenExists)
        {
            case AlreadyExistsActions.ThrowException:
                await users.InsertOneAsync(user);
                break;
            case AlreadyExistsActions.Skip:
                if ((await users.FindAsync(u => u.Id!.Equals(user.Id))).Any())
                    return;
                else await users.InsertOneAsync(user);
                break;
            case AlreadyExistsActions.Replace:
                var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
                await users.ReplaceOneAsync(filter, user);
                break;
        }
    }

    public async Task<DeleteResult> DeleteUserAsync(string id)
    {
        _ = await ClearToken(id);

        var filter_users = Builders<User>.Filter.Eq(r => r.Id, id);

        return await connector.GetCollection<User>("Users").DeleteOneAsync(filter_users);
    }

    public async Task<UserToken> GetOneTokenAsync(string id, string deviceId)
    {
        var tokens = connector.GetCollection<UserToken>("UsersTokens");

        var tokenQueried = await (await tokens.FindAsync(t => t.Id!.Equals(id))).FirstOrDefaultAsync();

        if (tokenQueried is null)
        {
            var token = new UserToken()
            {
                Id = id,
            }.UpdateOne(deviceId, out var newToken);
            await tokens.InsertOneAsync(token);
            return token;
        }
        else
        {
            if (tokenQueried.Tokens.ContainsKey(deviceId))
                tokenQueried.Refresh(deviceId);
            else tokenQueried.UpdateOne(deviceId, out _);

            var filter = Builders<UserToken>.Filter.Eq(u => u.Id, id);

            await tokens.ReplaceOneAsync(filter, tokenQueried);

            return tokenQueried;
        }
    }

    public async Task<bool> CheckToken(string id, string? token, string? deviceId)
    {
        if ((deviceId is null && !id.Equals("admin")) || token is null)
            return false;

        var tokens = connector.GetCollection<UserToken>("UsersTokens");

        var tokenQueried = (await tokens.FindAsync(
            t => t.Id!.Equals(id)
        ))
        .ToList()
        .Select(t => deviceId is null || t.CheckToken(deviceId!, token))
        .ToList();

        return tokenQueried.Count != 0;
    }

    public async Task<bool> CheckAdminToken(string token)
    {
        return await CheckToken("admin", token, null);
    }

    public async Task<long> ClearToken(string id)
    {
        var tokens = connector.GetCollection<UserToken>("UsersTokens");

        var filter_tokens = Builders<UserToken>.Filter.Eq(u => u.Id, id);

        return (await tokens.DeleteManyAsync(filter_tokens)).DeletedCount;
    }

    public List<User> GetUsers(int startIndex, int endIndex)
    {
        var users = connector.QueryCollection<User>("Users");
        var query = users.Skip(startIndex).Take(endIndex - startIndex + 1).ToList();
        return query;
    }
}