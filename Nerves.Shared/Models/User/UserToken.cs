using Nerves.Shared.Models.Auth;

namespace Nerves.Shared.Models.User;

public class UserToken
{
    public string? Id { get; set; }

    public Dictionary<string, Token> Tokens { get; set; } = [];

    public Token? GetToken(string deviceId)
    {
        if (Tokens.TryGetValue(deviceId, out var token))
            return token;
        else return null;
    }

    public UserToken UpdateOne(string deviceId, out string newToken)
    {
        newToken = Guid.NewGuid().ToString();
        if (!Tokens.TryAdd(deviceId, new(newToken)))
            Tokens[deviceId] = Tokens[deviceId].UpdateValue(newToken);
        return this;
    }

    public UserToken Refresh(string deviceId)
    {
        if (Tokens.TryGetValue(deviceId, out var token))
            token.ExpireTime = DateTime.Now + TimeSpan.FromDays(7);
        return this;
    }

    public bool CheckToken(string deviceId, string specificToken)
    {
        if (Tokens.TryGetValue(deviceId, out var existingToken))
            return existingToken.Value!.Equals(specificToken);
        else
            return false;
    }
}
