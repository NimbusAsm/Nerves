namespace Nerves.Shared.Models.User;

public class User
{
    public string? Id { get; set; }

    public string? NickName { get; set; }

    public DateTime? JoinTime { get; set; }

    public UserData? Data { get; set; }

    public UserSecurity? SecurityInfo { get; set; }

    public Dictionary<string, string>? Tags { get; set; }
}
