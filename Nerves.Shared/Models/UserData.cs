using static Nerves.Shared.Models.UserEnum;

namespace Nerves.Shared.Models;

public class UserData
{
    public DateTime? BirthDay { get; set; } = DateTime.Parse("2023.01.12");

    public Sex? Sex { get; set; } = UserEnum.Sex.Unknown;

    public string? Avatar { get; set; } = null;

    public string? Background { get; set; } = null;

    public string? Cover { get; set; } = null;

    public string? Location { get; set; } = "Earth";

    public string? Bio { get; set; } = null;

    public string? Website { get; set; } = null;

    public List<string>? Labels { get; set; } = null;

    public Dictionary<string, string>? Tags { get; set; } = null;
}
