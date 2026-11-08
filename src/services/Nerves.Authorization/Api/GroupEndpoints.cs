using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nerves.Authorization.Domain;
using Nerves.Authorization.Integration;
using Nerves.Authorization.Persistence;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Authorization.Api;

/// <summary>Management API for groups and their members.</summary>
public static class GroupEndpoints
{
    public static RouteGroupBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/groups")
            .RequireAuthorization(BearerAuthentication.AdminPolicy);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapGet("/{id:guid}/members", ListMembersAsync);
        group.MapPost("/{id:guid}/members", AddMemberAsync);
        group.MapDelete("/{id:guid}/members/{accountId:guid}", RemoveMemberAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        AuthorizationDbContext db,
        string? realm = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.Groups.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(realm))
        {
            query = query.Where(x => x.Realm == realm);
        }

        var groups = await query
            .OrderBy(x => x.Code)
            .Select(x => new { x.Id, x.Realm, x.Code, x.Name })
            .ToListAsync(cancellationToken);

        return Results.Ok(groups);
    }

    private static async Task<IResult> CreateAsync(
        CreateGroupRequest request,
        AuthorizationDbContext db,
        AuthorizationAuditClient audit,
        CancellationToken cancellationToken)
    {
        if (await db.Groups.AnyAsync(
                x => x.Realm == request.Realm && x.Code == request.Code,
                cancellationToken))
        {
            return Results.Problem("Group code already exists in this realm.", statusCode: StatusCodes.Status409Conflict);
        }

        var group = Group.Create(request.Realm, request.Code, request.Name, DateTimeOffset.UtcNow);
        db.Groups.Add(group);
        await db.SaveChangesAsync(cancellationToken);

        await audit.AppendAsync(
            "authz.group.created",
            "admin",
            "group",
            group.Id.ToString(),
            "{\"code\":\"" + group.Code + "\"}",
            cancellationToken);

        return Results.Created($"/api/v1/groups/{group.Id}", new { group.Id, group.Realm, group.Code, group.Name });
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        AuthorizationDbContext db,
        CancellationToken cancellationToken)
    {
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null)
        {
            return Results.NotFound();
        }

        var memberCount = await db.GroupMembers.CountAsync(x => x.GroupId == id, cancellationToken);
        return Results.Ok(new { group.Id, group.Realm, group.Code, group.Name, memberCount });
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        AuthorizationDbContext db,
        AuthorizationAuditClient audit,
        CancellationToken cancellationToken)
    {
        var group = await db.Groups.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null)
        {
            return Results.NotFound();
        }

        var members = await db.GroupMembers.Where(x => x.GroupId == id).ToListAsync(cancellationToken);
        db.GroupMembers.RemoveRange(members);
        db.Groups.Remove(group);

        foreach (var member in members)
        {
            await BumpAsync(db, member.AccountId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        await audit.AppendAsync(
            "authz.group.deleted",
            "admin",
            "group",
            id.ToString(),
            null,
            cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> ListMembersAsync(
        Guid id,
        AuthorizationDbContext db,
        CancellationToken cancellationToken)
    {
        var members = await db.GroupMembers.AsNoTracking()
            .Where(x => x.GroupId == id)
            .OrderBy(x => x.AddedAt)
            .Select(x => new { x.AccountId, x.AddedAt })
            .ToListAsync(cancellationToken);

        return Results.Ok(members);
    }

    private static async Task<IResult> AddMemberAsync(
        Guid id,
        AddGroupMemberRequest request,
        AuthorizationDbContext db,
        AuthorizationAuditClient audit,
        CancellationToken cancellationToken)
    {
        if (!await db.Groups.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return Results.NotFound();
        }

        if (!await db.GroupMembers.AnyAsync(
                x => x.GroupId == id && x.AccountId == request.AccountId,
                cancellationToken))
        {
            db.GroupMembers.Add(GroupMember.Create(id, request.AccountId, DateTimeOffset.UtcNow));
        }

        await BumpAsync(db, request.AccountId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await audit.AppendAsync(
            "authz.group.member_added",
            "admin",
            "account",
            request.AccountId.ToString(),
            "{\"groupId\":\"" + id + "\"}",
            cancellationToken);

        return Results.Created($"/api/v1/groups/{id}/members", new { groupId = id, request.AccountId });
    }

    private static async Task<IResult> RemoveMemberAsync(
        Guid id,
        Guid accountId,
        AuthorizationDbContext db,
        AuthorizationAuditClient audit,
        CancellationToken cancellationToken)
    {
        var member = await db.GroupMembers.FirstOrDefaultAsync(
            x => x.GroupId == id && x.AccountId == accountId,
            cancellationToken);

        if (member is null)
        {
            return Results.NotFound();
        }

        db.GroupMembers.Remove(member);
        await BumpAsync(db, accountId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await audit.AppendAsync(
            "authz.group.member_removed",
            "admin",
            "account",
            accountId.ToString(),
            "{\"groupId\":\"" + id + "\"}",
            cancellationToken);

        return Results.Ok();
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

/// <summary>Group creation payload.</summary>
public sealed record CreateGroupRequest(string Realm, string Code, string Name);

/// <summary>Group membership payload.</summary>
public sealed record AddGroupMemberRequest(Guid AccountId);
