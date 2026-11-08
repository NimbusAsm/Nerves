using Microsoft.EntityFrameworkCore;
using Nerves.Authorization.Domain;
using Nerves.Authorization.Persistence;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Authorization.Api;

/// <summary>
/// SCIM 2.0 group provisioning. Display names map to the group name; the code
/// comes from externalId or is derived from the display name.
/// </summary>
public static class ScimGroupsEndpoints
{
    private const string ListSchema = "urn:ietf:params:scim:api:messages:2.0:ListResponse";
    private const string GroupSchema = "urn:ietf:params:scim:schemas:core:2.0:Group";
    private const string ErrorSchema = "urn:ietf:params:scim:api:messages:2.0:Error";

    public static RouteGroupBuilder MapScimGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/scim/v2/Groups")
            .RequireAuthorization(BearerAuthentication.AdminPolicy);

        group.MapGet("", ListAsync);
        group.MapPost("", CreateAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPut("/{id:guid}", ReplaceAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        AuthorizationDbContext db,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.Groups.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                filter.Trim(),
                "^([A-Za-z0-9_.]+)\\s+eq\\s+(?:\"([^\"]*)\"|(\\S+))$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return Error("Only 'attribute eq \"value\"' filters are supported.", 400);
            }

            var attribute = match.Groups[1].Value;
            var value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;

            query = attribute.Equals("displayName", StringComparison.OrdinalIgnoreCase)
                ? query.Where(x => x.Name == value)
                : query.Where(x => x.Code == value);
        }

        var groups = await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            schemas = new[] { ListSchema },
            totalResults = groups.Count,
            startIndex = 1,
            itemsPerPage = groups.Count,
            Resources = groups.Select(g => ToScim(g, [])).ToArray(),
        });
    }

    private static async Task<IResult> CreateAsync(
        ScimGroupRequest request,
        AuthorizationDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Error("displayName is required.", 400);
        }

        var code = string.IsNullOrWhiteSpace(request.ExternalId)
            ? Slug(request.DisplayName)
            : request.ExternalId!;

        if (await db.Groups.AnyAsync(x => x.Realm == "nerves" && x.Code == code, cancellationToken))
        {
            return Error("Group already exists.", 409);
        }

        var group = Group.Create("nerves", code, request.DisplayName, DateTimeOffset.UtcNow);
        db.Groups.Add(group);

        foreach (var member in request.Members ?? [])
        {
            if (Guid.TryParse(member.Value, out var accountId))
            {
                db.GroupMembers.Add(GroupMember.Create(group.Id, accountId, DateTimeOffset.UtcNow));
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/scim/v2/Groups/{group.Id}", ToScim(group, request.Members ?? []));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        AuthorizationDbContext db,
        CancellationToken cancellationToken)
    {
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null)
        {
            return Error("Group not found.", 404);
        }

        var members = await db.GroupMembers.AsNoTracking()
            .Where(x => x.GroupId == id)
            .Select(x => x.AccountId.ToString())
            .ToListAsync(cancellationToken);

        return Results.Ok(ToScim(group, members.Select(m => new ScimMember(m)).ToArray()));
    }

    private static async Task<IResult> ReplaceAsync(
        Guid id,
        ScimGroupRequest request,
        AuthorizationDbContext db,
        CancellationToken cancellationToken)
    {
        var group = await db.Groups.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null)
        {
            return Error("Group not found.", 404);
        }

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            group.Rename(request.DisplayName);
        }

        var existing = await db.GroupMembers.Where(x => x.GroupId == id).ToListAsync(cancellationToken);
        db.GroupMembers.RemoveRange(existing);

        foreach (var member in request.Members ?? [])
        {
            if (Guid.TryParse(member.Value, out var accountId))
            {
                db.GroupMembers.Add(GroupMember.Create(id, accountId, DateTimeOffset.UtcNow));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToScim(group, request.Members ?? []));
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        AuthorizationDbContext db,
        CancellationToken cancellationToken)
    {
        var group = await db.Groups.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null)
        {
            return Error("Group not found.", 404);
        }

        var members = await db.GroupMembers.Where(x => x.GroupId == id).ToListAsync(cancellationToken);
        db.GroupMembers.RemoveRange(members);
        db.Groups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static object ToScim(Group group, IReadOnlyList<ScimMember> members) => new
    {
        schemas = new[] { GroupSchema },
        id = group.Id,
        displayName = group.Name,
        externalId = group.Code,
        members = members.Select(m => new { value = m.Value, display = m.Display }).ToArray(),
        meta = new { resourceType = "Group", created = group.CreatedAt },
    };

    private static string Slug(string displayName) =>
        new(displayName.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    private static IResult Error(string detail, int status) =>
        Results.Json(
            new { schemas = new[] { ErrorSchema }, detail, status, scimType = status == 404 ? "noTarget" : "invalidValue" },
            statusCode: status);
}

/// <summary>SCIM group payload.</summary>
public sealed record ScimGroupRequest(
    string? ExternalId,
    string DisplayName,
    ScimMember[]? Members);

/// <summary>One group member reference.</summary>
public sealed record ScimMember(string Value, string? Display = null);
