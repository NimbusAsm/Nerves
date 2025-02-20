namespace Nerves.Shared.Models.User.Security;

public class VerifiedFields
{
    public List<string>? VerifiedEmails { get; set; }

    public List<string>? VerifiedPhoneNumbers { get; set; }

    public List<UserDevice>? VerifiedDevices { get; set; }
}