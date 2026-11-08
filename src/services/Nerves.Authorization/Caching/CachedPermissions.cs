using Nerves.Authorization.Persistence;

namespace Nerves.Authorization.Caching;

/// <summary>
/// Version-aware read path shared by gRPC and the REST decision endpoint.
/// </summary>
public sealed class CachedPermissions(AuthorizationDbContext db, PermissionCache cache)
{
    public async Task<(IReadOnlyList<string> Permissions, long Version)> ResolveAsync(
        string accountId,
        string legalEntityId,
        CancellationToken cancellationToken)
    {
        var key = accountId + "|" + legalEntityId;

        // Read the cheap version first; a cached snapshot is reused only while it
        // still matches, so mutations invalidate entries without explicit flushes.
        var version = await PermissionResolver.ResolveVersionAsync(
            db, accountId, legalEntityId, cancellationToken);

        if (cache.TryGet(key, version, out var cached))
        {
            return (cached, version);
        }

        var (permissions, resolvedVersion) = await PermissionResolver.ResolveAsync(
            db, accountId, legalEntityId, cancellationToken);

        cache.Set(key, resolvedVersion, permissions);
        return (permissions, resolvedVersion);
    }
}
