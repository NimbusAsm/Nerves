namespace Nerves.Shared.Models;

public class UserToken
{
    public string? Id { get; set; }

    public string? Token { get; set; }

    public DateTime ExpireTime { get; set; } = DateTime.Now + TimeSpan.FromDays(7);
}
