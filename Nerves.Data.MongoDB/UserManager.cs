using MongoDB.Driver;
using Nerves.Shared.Configs.UsersConfigs.DataBaseOptions;
using Nerves.Shared.Models;

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
        var users = connector.GetCollection<User>("Nerves", "Users");
        var usersQueried = (await users.FindAsync(u => u.Id!.Equals(id))).ToList();
        return usersQueried.Count == 0 ? null : usersQueried.First();
    }

    public async void InsertUserAsync(User user, InsertUserOption? option = default)
    {
        option ??= new();

        var users = connector.GetCollection<User>("Nerves", "Users");

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
        var tokens = connector.GetCollection<UserToken>("Nerves", "UsersTokens");

        var filter_tokens = Builders<UserToken>.Filter.Eq(u => u.Id, id);

        _ = await tokens.DeleteManyAsync(filter_tokens);

        var filter_users = Builders<User>.Filter.Eq(r => r.Id, id);

        return await connector.GetCollection<User>("Nerves", "Users").DeleteOneAsync(filter_users);
    }

    public async Task<UserToken> GetOneTokenAsync(string id)
    {
        var tokens = connector.GetCollection<UserToken>("Nerves", "UsersTokens");

        var tokenQueried = await (await tokens.FindAsync(t => t.Id!.Equals(id))).FirstOrDefaultAsync();

        if (tokenQueried is null)
        {
            var token = new UserToken()
            {
                Id = id,
                Token = Guid.NewGuid().ToString(),
                ExpireTime = DateTime.Now + TimeSpan.FromDays(7)
            };
            await tokens.InsertOneAsync(token);
            return token;
        }
        else
        {
            tokenQueried.ExpireTime = DateTime.Now + TimeSpan.FromDays(7);

            var filter = Builders<UserToken>.Filter.Eq(u => u.Id, id);

            await tokens.ReplaceOneAsync(filter, tokenQueried);

            return tokenQueried;
        }
    }

    public async Task<bool> CheckToken(string id, string token)
    {
        var tokens = connector.GetCollection<UserToken>("Nerves", "UsersTokens");

        var tokenQueried = (await tokens.FindAsync(t => t.Id!.Equals(id) && t.Token!.Equals(token))).ToList();

        return tokenQueried.Count != 0;
    }
}