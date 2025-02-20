namespace Nerves.Shared.Models.Auth;

/* ## Initialization
 *
 * 1. User first sign in, generate a token T
 * 2. Use user's token encryption key to encrypt T and save value to KVdb
 * 3. User get the encrypted token and save it
 *
 * ### How to encrypt there ?
 *
 * 1. Every user was created with a `TokenEncryptionKey` which contains a RSA key set
 * 2. When create new token, we use the RSA key to 
 *
 *
 * ## Verification
 * 
 * 1. When user request any api that needs token, provide the token T2 (2 means remaining 2 times to use)
 * 2. Server will decrypt token with user's token encryption key
 * 3. If succeeded, it will reduce remaining usage count, and do the `Authentication` process
 * 4. If not succeeded, api returns bad request
 * 
 * ## Authentication
 * 
 * 1. If `Verification` passed, means the api requester is the actual user
 * 2. We need to check if user has the permission to call the api
 * 3. If not, return bad request
 * 4. If true, return api's result
 */

public struct Token
{
    public string? Value { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    public string OneTimeVerification { get; set; } = Guid.NewGuid().ToString();

    public int RemainingUsageCount { get; init; }

    public readonly bool CanConsume => RemainingUsageCount > 0;

    public Token(int usageCount = 3)
    {
        Value = Guid.NewGuid().ToString();
        RemainingUsageCount = usageCount;

        InitTimes();
    }

    private void InitTimes()
    {
        CreateTime = DateTime.Now;
        UpdateTime = CreateTime;
    }

    public Token Consume()
    {

        return this;
    }
}