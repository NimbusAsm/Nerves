namespace Nerves.Identity.Security;

/// <summary>Hashes and verifies account passwords.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
