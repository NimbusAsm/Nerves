using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nerves.Directory.Domain;
using Nerves.Directory.Integration;
using Nerves.Directory.Persistence;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Directory.Api;

/// <summary>Management API for organizations, members and invitations.</summary>
public static class OrganizationEndpoints
{
    public static RouteGroupBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization(Nerves.ServiceDefaults.Security.BearerAuthentication.AdminPolicy);

        group.MapGet("/organizations", ListAsync);
        group.MapPost("/organizations", CreateAsync);
        group.MapGet("/organizations/{id:guid}", GetAsync);
        group.MapGet("/organizations/{id:guid}/members", ListMembersAsync);
        group.MapPost("/organizations/{id:guid}/members", AddMemberAsync);
        group.MapDelete("/organizations/{id:guid}/members/{accountId:guid}", RemoveMemberAsync);
        group.MapPost("/organizations/{id:guid}/invitations", InviteAsync);
        group.MapPost("/invitations/{token}/accept", AcceptAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        DirectoryDbContext db,
        string? realm = null,
        int limit = 50,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var query = db.Organizations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(realm)) query = query.Where(x => x.Realm == realm);
        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out var cursorId))
        {
            query = query.Where(x => x.Id.CompareTo(cursorId) > 0);
        }

        var items = await query.OrderBy(x => x.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > limit;
        if (hasMore) items = items.Take(limit).ToList();

        return Results.Ok(new
        {
            items = items.Select(ToResponse),
            nextCursor = hasMore ? items[^1].Id.ToString() : null,
        });
    }

    private static async Task<IResult> CreateAsync(
        CreateOrganizationRequest request,
        DirectoryDbContext db,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var result = Organization.Create(request.Realm, request.Name, request.Slug, request.Description, request.ParentId, now);
        if (result.IsFailure)
        {
            return Results.Problem(result.Reason.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        var organization = result.Value;
        if (await db.Organizations.AnyAsync(
                x => x.Realm == organization.Realm && x.Slug == organization.Slug,
                cancellationToken))
        {
            return Results.Problem("Organization slug already exists in this realm.", statusCode: StatusCodes.Status409Conflict);
        }

        db.Organizations.Add(organization);
        AddAudit(db, "directory.organization.created", "organization", organization.Id.ToString(),
            "{\"slug\":\"" + organization.Slug + "\"}");
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/organizations/{organization.Id}", ToResponse(organization));
    }

    private static async Task<IResult> GetAsync(Guid id, DirectoryDbContext db, CancellationToken cancellationToken)
    {
        var organization = await db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return organization is null ? Results.NotFound() : Results.Ok(ToResponse(organization));
    }

    private static async Task<IResult> ListMembersAsync(
        Guid id,
        DirectoryDbContext db,
        CancellationToken cancellationToken)
    {
        var members = await db.Memberships.AsNoTracking()
            .Where(x => x.OrganizationId == id)
            .OrderBy(x => x.JoinedAt)
            .Select(x => new { x.Id, x.AccountId, x.Role, x.Status, x.JoinedAt })
            .ToListAsync(cancellationToken);

        return Results.Ok(members);
    }

    private static async Task<IResult> AddMemberAsync(
        Guid id,
        AddMemberRequest request,
        DirectoryDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await db.Organizations.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return Results.NotFound();
        }

        var existing = await db.Memberships.FirstOrDefaultAsync(
            x => x.OrganizationId == id && x.AccountId == request.AccountId, cancellationToken);

        if (existing is not null)
        {
            return Results.Problem("Account is already a member.", statusCode: StatusCodes.Status409Conflict);
        }

        var membership = Membership.Create(id, request.AccountId, request.Role, DateTimeOffset.UtcNow);
        db.Memberships.Add(membership);
        AddAudit(db, "directory.member.added", "account", request.AccountId.ToString(),
            "{\"organizationId\":\"" + id + "\"}");
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/v1/organizations/{id}/members",
            new { membership.Id, membership.AccountId, membership.Role, membership.Status });
    }

    private static async Task<IResult> RemoveMemberAsync(
        Guid id,
        Guid accountId,
        DirectoryDbContext db,
        CancellationToken cancellationToken)
    {
        var membership = await db.Memberships.FirstOrDefaultAsync(
            x => x.OrganizationId == id && x.AccountId == accountId, cancellationToken);

        if (membership is null)
        {
            return Results.NotFound();
        }

        membership.Remove();
        AddAudit(db, "directory.member.removed", "account", accountId.ToString(),
            "{\"organizationId\":\"" + id + "\"}");
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> InviteAsync(
        Guid id,
        InviteRequest request,
        DirectoryDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await db.Organizations.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return Results.NotFound();
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var tokenHash = Sha256(token);
        var emailHash = Sha256(request.Email.Trim().ToLowerInvariant());

        var invitation = Invitation.Create(id, emailHash, request.Role, tokenHash, DateTimeOffset.UtcNow, TimeSpan.FromDays(7));
        db.Invitations.Add(invitation);
        AddAudit(db, "directory.invitation.created", "organization", id.ToString(),
            "{\"role\":\"" + invitation.Role + "\"}");
        await db.SaveChangesAsync(cancellationToken);

        // The raw token is returned exactly once; only its hash is stored.
        return Results.Created(
            $"/api/v1/invitations/{invitation.Id}",
            new { invitation.Id, invitation.OrganizationId, invitation.Role, invitation.ExpiresAt, token });
    }

    private static async Task<IResult> AcceptAsync(
        string token,
        AcceptInvitationRequest request,
        DirectoryDbContext db,
        CancellationToken cancellationToken)
    {
        var tokenHash = Sha256(token);
        var invitation = await db.Invitations.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (invitation is null)
        {
            return Results.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var accept = invitation.Accept(request.AccountId, now);
        if (accept.IsFailure)
        {
            return Results.Problem(accept.Reason.Message, statusCode: StatusCodes.Status409Conflict);
        }

        if (await db.Memberships.AnyAsync(
                x => x.OrganizationId == invitation.OrganizationId && x.AccountId == request.AccountId,
                cancellationToken))
        {
            return Results.Problem("Account is already a member.", statusCode: StatusCodes.Status409Conflict);
        }

        db.Memberships.Add(Membership.Create(invitation.OrganizationId, request.AccountId, invitation.Role, now));
        AddAudit(db, "directory.invitation.accepted", "account", request.AccountId.ToString(),
            "{\"organizationId\":\"" + invitation.OrganizationId + "\"}");
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { invitation.OrganizationId, request.AccountId, invitation.Role });
    }


    private static void AddAudit(
        DirectoryDbContext db,
        string action,
        string? targetType,
        string? targetId,
        string detailJson)
    {
        db.OutboxEvents.Add(Domain.OutboxEvent.Create(
            action,
            targetType,
            targetId,
            detailJson,
            DateTimeOffset.UtcNow));
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static object ToResponse(Organization organization) => new
    {
        organization.Id,
        organization.Realm,
        organization.Name,
        organization.Slug,
        organization.Description,
        organization.ParentId,
        organization.CreatedAt,
    };
}

/// <summary>Organization creation payload.</summary>
public sealed record CreateOrganizationRequest(string Realm, string Name, string Slug, string? Description, Guid? ParentId);

/// <summary>Add-member payload.</summary>
public sealed record AddMemberRequest(Guid AccountId, string Role);

/// <summary>Invitation payload.</summary>
public sealed record InviteRequest(string Email, string Role);

/// <summary>Invitation acceptance payload.</summary>
public sealed record AcceptInvitationRequest(Guid AccountId);
