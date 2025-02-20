namespace Nerves.Shared.Models.User.Security;

public class PendingVerificationFields
{
    public string? PendingEmail { get; set; }

    public string? PendingPhoneNumber { get; set; }

    public UserDevice? PendingUserDevice { get; set; }
}