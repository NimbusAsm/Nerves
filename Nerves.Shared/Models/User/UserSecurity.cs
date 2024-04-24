namespace Nerves.Shared.Models.User;

public class UserSecurity
{
    public string? UserPasswordHash { get; set; }

    public string? UserPasswordHashSalt { get; set; }

    public List<string>? VerifiedEmails { get; set; }

    public string? PendingEmail { get; set; }

    public List<string>? VerifiedPhoneNumbers { get; set; }

    public string? PendingPhoneNumber { get; set; }

    public List<UserDevice>? VerifiedDevices { get; set; }

    public UserDevice? PendingUserDevice { get; set; }

    public string? TokenRsaEncryptionKey { get; set; }

    public Dictionary<string, string>? Tags { get; set; }
}
