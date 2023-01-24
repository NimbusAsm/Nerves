
namespace Nerves.API.Server.Models;

/// <summary>
/// 用户实体类
/// </summary>
public class User
{
    /// <summary>
    /// 用户 ID (主键)
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// 用户名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 用户创建时间
    /// </summary>
    public DateTime? JoinTime { get; set; }

    /// <summary>
    /// 最后一次登陆时间
    /// </summary>
    public DateTime? LastLoginTime { get; set; }

    /// <summary>
    /// 用户数据
    /// </summary>
    public UserData? Data { get; set; }

    /// <summary>
    /// 安全信息
    /// </summary>
    public UserSecurity? SecurityInfo { get; set; }

    /// <summary>
    /// 是否在线
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// 标记
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }
}
