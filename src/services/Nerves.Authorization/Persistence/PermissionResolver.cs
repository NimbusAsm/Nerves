using Microsoft.EntityFrameworkCore;
using Nerves.Authorization.Domain;

namespace Nerves.Authorization.Persistence;

/// <summary>Resolves a subject's effective permissions and version.</summary>
public static class PermissionResolver
{
    /// <summary>Maximum version across the account, its groups and the legal entity.</summary>
    public static async Task<long> ResolveVersionAsync(
        AuthorizationDbContext db,
        string accountId,
        string legalEntityId,
        CancellationToken cancellationToken)
    {
        var subjects = await CollectSubjectsAsync(db, accountId, legalEntityId, cancellationToken);

        if (subjects.Count == 0)
        {
            return 0;
        }

        return await db.SubjectVersions
            .Where(x => subjects.Contains(x.SubjectId))
            .Select(x => (long?)x.Version)
            .MaxAsync(cancellationToken) ?? 0;
    }

    public static async Task<(IReadOnlyList<string> Permissions, long Version)> ResolveAsync(
        AuthorizationDbContext db,
        string accountId,
        string legalEntityId,
        CancellationToken cancellationToken)
    {
        var subjects = await CollectSubjectsAsync(db, accountId, legalEntityId, cancellationToken);

        if (subjects.Count == 0)
        {
            return ([], 0);
        }

        var roleIds = await db.RoleAssignments
            .Where(x => subjects.Contains(x.SubjectId))
            .Select(x => x.RoleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var permissions = await (
                from rp in db.Set<RolePermission>()
                join p in db.Permissions on rp.PermissionId equals p.Id
                where roleIds.Contains(rp.RoleId)
                select p.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        return (permissions, await ResolveVersionAsync(db, accountId, legalEntityId, cancellationToken));
    }

    private static async Task<List<Guid>> CollectSubjectsAsync(
        AuthorizationDbContext db,
        string accountId,
        string legalEntityId,
        CancellationToken cancellationToken)
    {
        var subjects = new List<Guid>();

        if (Guid.TryParse(accountId, out var account) && account != Guid.Empty)
        {
            subjects.Add(account);
        }

        if (Guid.TryParse(legalEntityId, out var entity) && entity != Guid.Empty)
        {
            subjects.Add(entity);
        }

        if (Guid.TryParse(accountId, out account) && account != Guid.Empty)
        {
            var groupIds = await db.GroupMembers
                .Where(x => x.AccountId == account)
                .Select(x => x.GroupId)
                .ToListAsync(cancellationToken);

            subjects.AddRange(groupIds);
        }

        return subjects;
    }
}
