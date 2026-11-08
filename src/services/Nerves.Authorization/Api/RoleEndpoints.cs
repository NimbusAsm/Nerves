using Microsoft.EntityFrameworkCore;
using Nerves.Authorization.Domain;
using Nerves.Authorization.Persistence;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Authorization.Api;

/// <summary>Management API for roles and assignments.</summary>
public static class RoleEndpoints
{
    public static RouteGroupBuilder MapAuthorizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization(Nerves.ServiceDefaults.Security.BearerAuthentication.AdminPolicy);

        group.MapGet("/roles", ListRolesAsync);
        group.MapPost("/roles", CreateRoleAsync);
        group.MapPost("/roles/{id:guid}/assign", AssignAsync);
        group.MapPost("/roles/{id:guid}/revoke", RevokeAsync);
        group.MapGet("/permissions", ListPermissionsAsync);
        group.MapPost("/permissions", CreatePermissionAsync);

        return group;
    }

    private static async Task<IResult> ListRolesAsync(
        AuthorizationDbContext db,
        string? realm = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.Roles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(realm))
        {
            query = query.Where(x => x.Realm == realm);
        }

        var roles = await query
            .OrderBy(x => x.Code)
            .Select(x => new { x.Id, x.Realm, x.Code, x.Name })
            .ToListAsync(cancellationToken);

        return Results.Ok(roles);
    }

    private static async Task<IResult> CreateRoleAsync(
        CreateRoleRequest request,
        AuthorizationDbContext db,
        CancellationToken cancellationToken)
    {
        if (await db.Roles.AnyAsync(x => x.Realm == request.Realm && x.Code == request.Code, cancellationToken))
        {
            return Results.Problem("Role already exists.", statusCode: StatusCodes.Status409Conflict);
        }

        var role = Role.Create(request.Realm, request.Code, request.Name, request.ApplicationId, DateTimeOffset.UtcNow);
        db.Roles.Add(role);

        if (request.PermissionCodes is { Count: > 0 })
        {
            foreach (var code in request.PermissionCodes)
            {
                var permission = await db.Permissions.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
                if (permission is null)
                {
                    permission = Permission.Create(request.ApplicationId, code, code);
                    db.Permissions.Add(permission);
                }

                db.Set<RolePermission>().Add(RolePermission.Create(role.Id, permission.Id));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/roles/{role.Id}", new { role.Id, role.Realm, role.Code, role.Name });
    }

    private static async Task<IResult> AssignAsync(
        Guid id,
        AssignRoleRequest request,
        AuthorizationDbContext db,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SubjectType>(request.SubjectType, ignoreCase: true, out var subjectType))
        {
            return Results.Problem("Unknown subject type.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!await db.Roles.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return Results.NotFound();
        }

        if (!await db.RoleAssignments.AnyAsync(
                x => x.RoleId == id && x.SubjectId == request.SubjectId && x.SubjectType == subjectType,
                cancellationToken))
        {
            db.RoleAssignments.Add(RoleAssignment.Create(id, subjectType, request.SubjectId, DateTimeOffset.UtcNow));
        }

        await BumpAsync(db, request.SubjectId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> RevokeAsync(
        Guid id,
        AssignRoleRequest request,
        AuthorizationDbContext db,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SubjectType>(request.SubjectType, ignoreCase: true, out var subjectType))
        {
            return Results.Problem("Unknown subject type.", statusCode: StatusCodes.Status400BadRequest);
        }

        var assignment = await db.RoleAssignments.FirstOrDefaultAsync(
            x => x.RoleId == id && x.SubjectId == request.SubjectId && x.SubjectType == subjectType,
            cancellationToken);

        if (assignment is null)
        {
            return Results.NotFound();
        }

        db.RoleAssignments.Remove(assignment);
        await BumpAsync(db, request.SubjectId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> ListPermissionsAsync(
        AuthorizationDbContext db,
        CancellationToken cancellationToken)
    {
        var permissions = await db.Permissions.AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new { x.Id, x.Code, x.Description })
            .ToListAsync(cancellationToken);

        return Results.Ok(permissions);
    }

    private static async Task<IResult> CreatePermissionAsync(
        CreatePermissionRequest request,
        AuthorizationDbContext db,
        CancellationToken cancellationToken)
    {
        if (await db.Permissions.AnyAsync(x => x.Code == request.Code, cancellationToken))
        {
            return Results.Problem("Permission already exists.", statusCode: StatusCodes.Status409Conflict);
        }

        var permission = Permission.Create(null, request.Code, request.Description);
        db.Permissions.Add(permission);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/permissions/{permission.Id}", new { permission.Id, permission.Code });
    }

    private static async Task BumpAsync(AuthorizationDbContext db, Guid subjectId, CancellationToken cancellationToken)
    {
        var version = await db.SubjectVersions.FirstOrDefaultAsync(x => x.SubjectId == subjectId, cancellationToken);
        if (version is null)
        {
            db.SubjectVersions.Add(SubjectVersion.Create(subjectId));
        }
        else
        {
            version.Bump();
        }
    }
}

/// <summary>Role creation payload.</summary>
public sealed record CreateRoleRequest(string Realm, string Code, string Name, Guid? ApplicationId, List<string>? PermissionCodes);

/// <summary>Assignment payload.</summary>
public sealed record AssignRoleRequest(string SubjectType, Guid SubjectId);

/// <summary>Permission creation payload.</summary>
public sealed record CreatePermissionRequest(string Code, string Description);
