using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nerves.Authorization.Domain;
using Nerves.Authorization.Persistence;

namespace Nerves.Authorization.Seeding;

/// <summary>Creates the management permission, role and assignments on startup.</summary>
public sealed class AuthorizationDevSeeder(
    IServiceProvider services,
    IOptions<DevSeedOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var seed = options.Value;
        if (!seed.Enabled)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthorizationDbContext>();

        var permission = await db.Permissions.FirstOrDefaultAsync(x => x.Code == seed.PermissionCode, cancellationToken);
        if (permission is null)
        {
            permission = Permission.Create(null, seed.PermissionCode, "Nerves management access");
            db.Permissions.Add(permission);
            await db.SaveChangesAsync(cancellationToken);
        }

        var role = await db.Roles.FirstOrDefaultAsync(
            x => x.Realm == seed.Realm && x.Code == seed.RoleCode, cancellationToken);

        if (role is null)
        {
            role = Role.Create(seed.Realm, seed.RoleCode, "Portal administrator", null, DateTimeOffset.UtcNow);
            db.Roles.Add(role);
            db.Set<RolePermission>().Add(RolePermission.Create(role.Id, permission.Id));
            await db.SaveChangesAsync(cancellationToken);
        }

        foreach (var accountId in seed.AdminAccountIds)
        {
            if (await db.RoleAssignments.AnyAsync(
                    x => x.RoleId == role.Id && x.SubjectType == SubjectType.Account && x.SubjectId == accountId,
                    cancellationToken))
            {
                continue;
            }

            db.RoleAssignments.Add(RoleAssignment.Create(role.Id, SubjectType.Account, accountId, DateTimeOffset.UtcNow));

            var version = await db.SubjectVersions.FirstOrDefaultAsync(x => x.SubjectId == accountId, cancellationToken);
            if (version is null)
            {
                db.SubjectVersions.Add(SubjectVersion.Create(accountId));
            }
            else
            {
                version.Bump();
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
