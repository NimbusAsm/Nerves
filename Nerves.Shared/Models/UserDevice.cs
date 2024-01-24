namespace Nerves.Shared.Models;

public class UserDevice
{
    public string? Name { get; set; }

    public string? Type { get; set; }

    public string? OS { get; set; }

    public string? OSVersion { get; set; }

    public DateTime? LastLoginTime { get; set; }

    public bool IsOnline { get; set; }

    public string? Token { get; set; }

    public string? TokenHash { get; set; }

    public string? Ip { get; set; }

    public string? MacAddress { get; set; }

    public Dictionary<string, string>? Tags { get; set; }
}
