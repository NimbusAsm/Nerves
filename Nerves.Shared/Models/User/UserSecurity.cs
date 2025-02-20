using Nerves.Shared.Models.User.Security;

namespace Nerves.Shared.Models.User;

public class UserSecurity
{
    public string? UserPasswordHash { get; set; }

    public string? UserPasswordHashSalt { get; set; }

    public VerifiedFields? VerifiedFields { get; set; }

    public PendingVerificationFields? PendingVerificationFields { get; set; }

    public Dictionary<string, string>? Tags { get; set; }
}
