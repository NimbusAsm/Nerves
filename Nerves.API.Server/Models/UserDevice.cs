
namespace Nerves.API.Server.Models;

/// <summary>
/// 用户设备实体类
/// </summary>
public class UserDevice
{
    /// <summary>
    /// 设备名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 设备类型
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 设备操作系统
    /// </summary>
    public string? OS { get; set; }

    /// <summary>
    /// 设备操作系统版本
    /// </summary>
    public string? OSVersion { get; set; }

    /// <summary>
    /// 此设备最后一次登陆时间
    /// </summary>
    public DateTime? LastLoginTime { get; set; }

    /// <summary>
    /// 该设备是否在线
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// 设备 Token
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// 设备 Token 哈希
    /// </summary>
    public string? TokenHash { get; set; }

    /// <summary>
    /// IP 地址
    /// </summary>
    public string? Ip { get; set; }

    /// <summary>
    /// 设备 MAC 地址
    /// </summary>
    public string? MacAddress { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }
}
