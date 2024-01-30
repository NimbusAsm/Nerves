namespace Nerves.Shared.Models.Auth;

public struct Token
{
    public string? Value { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime ExpireTime { get; set; }

    public Token()
    {

    }

    public Token(string value)
    {
        Value = value;
        CreateTime = DateTime.Now;
        ExpireTime = DateTime.Now + TimeSpan.FromDays(7);
    }

    public Token UpdateValue(string value)
    {
        Value = value;
        ExpireTime = DateTime.Now + TimeSpan.FromDays(7);
        return this;
    }
}