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
/// deactivate, which is what provisioning clients exercise. Accounts are
/// sharded, so lookups fan out and each write targets the owning shard.
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
        IdentityStore store,
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

        // Every shard is queried; filters that reference identifiers resolve to
        // account ids first so the per-shard query stays a primary-key lookup.
        var ids = await ResolveFilterAsync(store, parsed, cancellationToken);
        if (ids is not null && ids.Count == 0)
        {
            return Results.Ok(new ScimListResponse<ScimUser>
            {
                TotalResults = 0,
                StartIndex = startIndex,
                ItemsPerPage = 0,
                Resources = [],
            });
        }

        var accounts = await store.QueryShardsAsync(async db =>
        {
            var query = db.NetworkAccounts.AsNoTracking();
            if (ids is not null)
            {
                query = query.Where(x => ids.Contains(x.Id));
            }

            return await query.OrderBy(x => x.Username).ToListAsync(cancellationToken);
        }, cancellationToken);

        var ordered = accounts.OrderBy(x => x.Username, StringComparer.Ordinal).ToList();
        var page = ordered.Skip(startIndex - 1).Take(count).ToList();

        return Results.Ok(new ScimListResponse<ScimUser>
        {
            TotalResults = ordered.Count,
            StartIndex = startIndex,
            ItemsPerPage = page.Count,
            Resources = page.Select(ToScim).ToArray(),
        });
    }

    private static async Task<List<Guid>?> ResolveFilterAsync(
        IdentityStore store,
        ScimFilter filter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(filter.Attribute))
        {
            return null;
        }

        var attribute = filter.Attribute.ToLowerInvariant();

        return attribute switch
        {
            "username" or "externalid" => await store.QueryShardsAsync(async db => await db.NetworkAccounts
                .Where(x => x.Username == filter.Value)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken), cancellationToken),
            _ => [],
        };
    }

    private static async Task<IResult> CreateAsync(
        ScimUser request,
        IdentityStore store,
        IPasswordHasher hasher,
        IIdentifierHasher identifiers,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return Problem("userName is required.", StatusCodes.Status400BadRequest);
        }

        if (await store.FindByUsernameAsync("nerves", request.UserName, cancellationToken) is not null)
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
        var credential = AccountCredential.Create(
            account.Value.Id,
            CredentialType.Password,
            hasher.Hash(password),
            Pbkdf2PasswordHasher.Algorithm,
            now).Value;

        var indexes = new List<RoutingIndexEntry>
        {
            RoutingIndexEntry.Create(
                identifiers.Hash("username", "nerves", request.UserName),
                "username",
                "nerves",
                account.Value.Id,
                account.Value.LegalEntityId,
                store.ShardFor(account.Value.Id),
                now),
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            indexes.Add(RoutingIndexEntry.Create(
                identifiers.Hash("email", "nerves", email),
                "email",
                "nerves",
                account.Value.Id,
                account.Value.LegalEntityId,
                store.ShardFor(account.Value.Id),
                now));
        }

        var created = await store.CreateAccountAsync(account.Value, credential, indexes, cancellationToken);
        if (created.IsFailure)
        {
            return Problem(created.Reason.Message, StatusCodes.Status409Conflict);
        }

        return Results.Created($"/scim/v2/Users/{account.Value.Id}", ToScim(account.Value));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IdentityStore store,
        CancellationToken cancellationToken)
    {
        await using var db = store.AccountShard(id);
        var account = await db.NetworkAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return account is null ? NotFound(id) : Results.Ok(ToScim(account));
    }

    private static async Task<IResult> ReplaceAsync(
        Guid id,
        ScimUser request,
        IdentityStore store,
        CancellationToken cancellationToken)
    {
        await using var db = store.AccountShard(id);
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
        IdentityStore store,
        CancellationToken cancellationToken)
    {
        await using var db = store.AccountShard(id);
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

    private static IResult Problem(string detail, int statusCode = StatusCodes.Status400BadRequest) =>
        Results.Problem(detail, statusCode: statusCode);

    private static IResult NotFound(Guid id) => Results.Problem(
        type: "about:blank",
        detail: "User " + id + " not found",
        statusCode: StatusCodes.Status404NotFound);
}
