namespace Nerves.Identity.Integration;

/// <summary>Session revocation port, replaceable in tests.</summary>
public interface ISessionRevoker
{
    Task<int> RevokeAllAsync(string accountId, string reason, CancellationToken cancellationToken);
}
