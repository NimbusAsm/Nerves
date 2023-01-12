using static Nerves.API.Server.Rules.UserEnum;

namespace Nerves.API.Server.Models;

public class UserData
{
    public DateTime? BirthDay { get; set; }

    public Sex? Sex { get; set; }

    public string? Avatar { get; set; }

    public string? Background { get; set; }

    public string? Cover { get; set; }

    public string? Location { get; set; }

    public string? Bio { get; set; }

    public string? Website { get; set; }

    public List<string>? Labels { get; set; }

    public Dictionary<string, string>? Tags { get; set; }

}
