namespace Nerves.Identity.Security;

/// <summary>Turns a login identifier into a stable, non-reversible lookup key.</summary>
public interface IIdentifierHasher
{
    string Hash(string identifierType, string realm, string identifier);
}
