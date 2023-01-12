
namespace Nerves.API.Server.Models;

public class User
{
    public string? Name { get; set; }

    public string? DisplayName { get; set; }

    public DateTime? JoinTime { get; set; }

    public DateTime? LastLoginTime { get; set; }

    public UserSecurity? SecurityInfo { get; set; }

    public bool IsOnline { get; set; }

    public Dictionary<string, string>? Tags { get; set; }
}
