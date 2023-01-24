using static Nerves.API.Server.Rules.UserEnum;

namespace Nerves.API.Server.Models;

/// <summary>
/// 用户数据实体类
/// </summary>
public class UserData
{
    /// <summary>
    /// 用户生日
    /// </summary>
    public DateTime? BirthDay { get; set; } = DateTime.Parse("2023.01.12");

    /// <summary>
    /// 用户性别
    /// </summary>
    public Sex? Sex { get; set; } = Rules.UserEnum.Sex.Unknown;

    /// <summary>
    /// 用户头像链接
    /// </summary>
    public string? Avatar { get; set; } = null;

    /// <summary>
    /// 用户背景图片链接
    /// </summary>
    public string? Background { get; set; } = null;

    /// <summary>
    /// 用户封面图片链接
    /// </summary>
    public string? Cover { get; set; } = null;

    /// <summary>
    /// 用户所在地
    /// </summary>
    public string? Location { get; set; } = "Earth";

    /// <summary>
    /// Bio
    /// </summary>
    public string? Bio { get; set; } = null;

    /// <summary>
    /// 用户网站链接
    /// </summary>
    public string? Website { get; set; } = null;

    /// <summary>
    /// 用户兴趣标签
    /// </summary>
    public List<string>? Labels { get; set; } = null;

    /// <summary>
    /// 标记
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; } = null;

}
