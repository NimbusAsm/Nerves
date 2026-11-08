using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Domain;
using Nerves.Identity.Persistence;
using Nerves.Identity.Scim;
using Nerves.Identity.Security;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Identity.Api;

/// <summary>
/// SCIM 2.0 user provisioning so external identity providers can push users.
/// The subset covers create, read, list with a simple eq filter, replace and
/// deactivate, which is what provisioning clients exercise.
/// </summary>
public static class ScimUsersEndpoints
{
    public static RouteGroupBuilder MapScimUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/scim/v2/Users")
            .RequireAuthorization(BearerAuthentication.AdminPolicy);

        group.MapGet("", ListAsync);
        group.MapPost("", CreateAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPut("/{id:guid}", ReplaceAsync);
        group.MapDelete("/{id:guid}", DeactivateAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        IdentityDbContext db,
        string? filter = null,
        int startIndex = 1,
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        if (!ScimFilter.TryParse(filter, out var parsed, out var error))
        {
            return Problem(error!);
        }

        startIndex = Math.Max(startIndex, 1);
        count = Math.Clamp(count, 1, 200);

        var query = db.NetworkAccounts.AsNoTracking();
        var ids = await ResolveFilterAsync(db, parsed, cancellationToken);

        if (ids is not null)
        {
            query = query.Where(x => ids.Contains(x.Id));
        }

        var total = await query.CountAsync(cancellationToken);
        var accounts = await query
            .OrderBy(x => x.Username)
            .Skip(startIndex - 1)
            .Take(count)
            .ToListAsync(cancellationToken);

        return Results.Ok(new ScimListResponse<ScimUser>
        {
            TotalResults = total,
            StartIndex = startIndex,
            ItemsPerPage = accounts.Count,
            Resources = accounts.Select(ToScim).ToArray(),
        });
    }

    private static Task<List<Guid>?> ResolveFilterAsync(
        IdentityDbContext db,
        ScimFilter filter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(filter.Attribute))
        {
            return Task.FromResult<List<Guid>?>(null);
        }

        var attribute = filter.Attribute.ToLowerInvariant();

        return attribute switch
        {
            "username" or "externalid" => db.NetworkAccounts
                .Where(x => x.Username == filter.Value)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken)
                .ContinueWith(t => (List<Guid>?)t.Result, TaskScheduler.Default),
            _ => Task.FromResult<List<Guid>?>([]),
        };
    }

    private static async Task<IResult> CreateAsync(
        ScimUser request,
        IdentityDbContext db,
        IPasswordHasher hasher,
        IIdentifierHasher identifiers,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return Problem("userName is required.", StatusCodes.Status400BadRequest);
        }

        if (await db.NetworkAccounts.AnyAsync(x => x.Username == request.UserName, cancellationToken))
        {
            return Problem("userName is already taken.", StatusCodes.Status409Conflict);
        }

        var now = DateTimeOffset.UtcNow;
        var email = request.Emails.FirstOrDefault()?.Value;

        var account = NetworkAccount.Create(
            null,
            "nerves",
            request.UserName,
            email,
            null,
            request.DisplayName ?? request.Name?.Formatted ?? request.UserName,
            AccountType.Service,
            now);

        if (account.IsFailure)
        {
            return Problem(account.Reason.Message, StatusCodes.Status400BadRequest);
        }

        var password = Guid.NewGuid().ToString("N") + "aA1!";
        db.NetworkAccounts.Add(account.Value);
        db.AccountCredentials.Add(AccountCredential.Create(
            account.Value.Id,
            CredentialType.Password,
            hasher.Hash(password),
            Pbkdf2PasswordHasher.Algorithm,
            now).Value);

        if (!string.IsNullOrWhiteSpace(email))
        {
            db.RoutingIndex.Add(RoutingIndexEntry.Create(
                identifiers.Hash("username", "nerves", request.UserName),
                "username",
                "nerves",
                account.Value.Id,
                account.Value.LegalEntityId,
                now));
        }

        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/scim/v2/Users/{account.Value.Id}", ToScim(account.Value));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var account = await db.NetworkAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return account is null ? NotFound(id) : Results.Ok(ToScim(account));
    }

    private static async Task<IResult> ReplaceAsync(
        Guid id,
        ScimUser request,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var account = await db.NetworkAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (account is null)
        {
            return NotFound(id);
        }

        if (!request.Active)
        {
            account.Disable(DateTimeOffset.UtcNow);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToScim(account));
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var account = await db.NetworkAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (account is null)
        {
            return NotFound(id);
        }

        account.Disable(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static ScimUser ToScim(NetworkAccount account) => new()
    {
        Id = account.Id.ToString(),
        UserName = account.Username,
        DisplayName = account.DisplayName,
        Name = new ScimName(account.DisplayName),
        Emails = string.IsNullOrWhiteSpace(account.Email)
            ? []
            : [new ScimEmail(account.Email!)],
        Active = account.Status == AccountStatus.Active,
        Meta = new ScimMeta
        {
            Created = account.CreatedAt,
            LastModified = account.UpdatedAt,
        },
    };

    private static IResult Problem(string detail, int status = StatusCodes.Status400BadRequest) =>
        Results.Json(new ScimError { Detail = detail, Status = status, ScimType = "invalidValue" }, statusCode: status);

    private static IResult NotFound(Guid id) =>
        Results.Json(new ScimError { Detail = "User " + id + " not found.", Status = 404, ScimType = "noTarget" }, statusCode: 404);
}
