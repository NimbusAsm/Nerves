namespace Nerves.Shared.Models;

public class UserToken
{
    public string? Id { get; set; }

    public string? Token { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.Now;

    public DateTime ExpireTime { get; set; } = DateTime.Now + TimeSpan.FromDays(7);

    public DateTime UpdatedTime { get; set; } = DateTime.Now;
}
