namespace Nerves.Shared.Models;

public class User
{
    public int? Id { get; set; }

    public string? Name { get; set; }

    public DateTime? JoinTime { get; set; }

    public DateTime? LastLoginTime { get; set; }

    public UserData? Data { get; set; }

    public UserSecurity? SecurityInfo { get; set; }

    public bool IsOnline { get; set; }

    public Dictionary<string, string>? Tags { get; set; }
}
