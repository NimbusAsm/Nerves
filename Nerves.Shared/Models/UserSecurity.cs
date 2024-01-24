namespace Nerves.Shared.Models;

public class UserSecurity
{
    public string? UserPasswordHash { get; set; }

    public string? UserPasswordHashSalt { get; set; }

    public string? UserToken { get; set; }

    public string? UserTokenHash { get; set; }

    public List<string>? VerifiedEmail { get; set; }

    public string? PendingEmail { get; set; }

    public List<string>? VerifiedPhoneNumber { get; set; }

    public string? PendingPhoneNumber { get; set; }

    public List<UserDevice>? VerifiedDevices { get; set; }

    public UserDevice? PendingUserDevice { get; set; }

    public Dictionary<string, string>? Tags { get; set; }
}
