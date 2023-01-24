namespace Nerves.API.Server.Models;

/// <summary>
/// 用户安全信息实体类
/// </summary>
public class UserSecurity
{
    /// <summary>
    /// 用户密码 Hash 值 (加盐)
    /// </summary>
    public string? UserPasswordHash { get; set; }

    /// <summary>
    /// 用户 Token
    /// </summary>
    public string? UserToken { get; set; }

    /// <summary>
    /// 用户 Token 哈希值
    /// </summary>
    public string? UserTokenHash { get; set; }

    /// <summary>
    /// 已验证的邮箱地址列表
    /// </summary>
    public List<string>? VerifiedEmail { get; set; }

    /// <summary>
    /// 等待验证的邮箱地址 (只能同时验证一个邮箱)
    /// </summary>
    public string? PendingEmail { get; set; }

    /// <summary>
    /// 已验证的电话号码列表
    /// </summary>
    public List<string>? VerifiedPhoneNumber { get; set; }

    /// <summary>
    /// 等待验证的电话号码 (只能同时验证一个电话号码)
    /// </summary>
    public string? PendingPhoneNumber { get; set; }

    /// <summary>
    /// 已验证的设备列表
    /// </summary>
    public List<UserDevice>? VerifiedDevices { get; set; }

    /// <summary>
    /// 等待验证的设备 (只能同时验证一个设备)
    /// </summary>
    public UserDevice? PendingUserDevice { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }
}
