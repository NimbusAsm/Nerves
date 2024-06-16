using static Nerves.Shared.Models.User.UserEnum;

namespace Nerves.Shared.Models.User;

public class UserData
{
    public DateTime? BirthDay { get; set; }

    public DateTime? JoinTime { get; set; }

    public Sex? Sex { get; set; } = UserEnum.Sex.Unknown;

    public string? Avatar { get; set; }

    public string? Background { get; set; }

    public string? Banner { get; set; }

    public string? Cover { get; set; }

    public string? Location { get; set; }

    public string? Bio { get; set; }

    public List<UserLink>? Links { get; set; }

    public List<UserLable>? Labels { get; set; }

    public Dictionary<string, string>? Tags { get; set; } = null;
}
