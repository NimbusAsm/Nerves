namespace Nerves.Shared.Models.User.Security;

public class TokenEncryptionKey
{
    public string? RsaPublicKeyPem { get; set; }

    public string? RsaPrivateKeyPem { get; set; }
}