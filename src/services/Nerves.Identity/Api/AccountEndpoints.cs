using Microsoft.EntityFrameworkCore;
using Nerves.Core;
using Nerves.Identity.Domain;
using Nerves.Identity.Integration;
using Nerves.Identity.Persistence;
using Nerves.Identity.Security;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Identity.Api;

/// <summary>Management API for network accounts.</summary>
public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/accounts").RequireAuthorization(Nerves.ServiceDefaults.Security.BearerAuthentication.AdminPolicy);

        group.MapPost("/", CreateAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/{id:guid}/disable", DisableAsync);
        group.MapPost("/{id:guid}/lock", LockAsync);
        group.MapPost("/{id:guid}/password", ChangePasswordAsync);

        return group;
    }

    private static async Task<IResult> CreateAsync(
        CreateAccountRequest request,
        IdentityDbContext db,
        IPasswordHasher hasher,
        IIdentifierHasher identifiers,
        Nerves.Identity.Sharding.ShardResolver shards,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<AccountType>(request.LegalEntityId is null ? nameof(AccountType.Service) : nameof(AccountType.Personal), out var type))
        {
            return Results.Problem("Invalid account type.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!string.IsNullOrWhiteSpace(request.LegalEntityId?.ToString())
            && !await db.LegalEntities.AnyAsync(x => x.Id == request.LegalEntityId, cancellationToken))
        {
            return Results.Problem("Legal entity not found.", statusCode: StatusCodes.Status400BadRequest);
        }

        var now = DateTimeOffset.UtcNow;
        var accountResult = NetworkAccount.Create(
            request.LegalEntityId,
            request.Realm,
            request.Username,
            request.Email,
            request.PhoneNumber,
            request.DisplayName,
            type,
            now);

        if (accountResult.IsFailure)
        {
            return Results.Problem(accountResult.Reason.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        var account = accountResult.Value;

        var secret = string.IsNullOrEmpty(request.Password) ? Guid.NewGuid().ToString("N") : request.Password;
        var credentialResult = AccountCredential.Create(
            account.Id, CredentialType.Password, hasher.Hash(secret), Pbkdf2PasswordHasher.Algorithm, now);

        if (credentialResult.IsFailure)
        {
            return Results.Problem(credentialResult.Reason.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        var indexes = new List<RoutingIndexEntry>();
        foreach (var (identifierType, value) in new (string, string?)[]
                 {
                     ("username", request.Username),
                     ("email", request.Email),
                     ("phone", request.PhoneNumber),
                 })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var hash = identifiers.Hash(identifierType, request.Realm, value);
            if (await db.RoutingIndex.AnyAsync(x => x.IdentifierHash == hash, cancellationToken))
            {
                return Results.Problem(
                    $"The {identifierType} is already in use.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            indexes.Add(RoutingIndexEntry.Create(hash, identifierType, request.Realm, account.Id, account.LegalEntityId, shards.Resolve(account.Id), now));
        }

        db.NetworkAccounts.Add(account);
        db.AccountCredentials.Add(credentialResult.Value);
        db.RoutingIndex.AddRange(indexes);

        await db.SaveChangesAsync(cancellationToken);

        await audit.AppendAsync(
            "account.created",
            "admin",
            null,
            "account",
            account.Id.ToString(),
            "success",
            http.TraceIdentifier,
            null,
            cancellationToken);

        return Results.Created(
            $"/api/v1/accounts/{account.Id}",
            ToResponse(account));
    }

    private static async Task<IResult> ListAsync(
        IdentityDbContext db,
        int limit = 50,
        string? cursor = null,
        string? status = null,
        string? realm = null,
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        var accounts = db.NetworkAccounts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(realm))
        {
            accounts = accounts.Where(x => x.Realm == realm);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AccountStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            accounts = accounts.Where(x => x.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            accounts = accounts.Where(x => EF.Functions.ILike(x.Username, $"%{query}%"));
        }

        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out var cursorId))
        {
            accounts = accounts.Where(x => x.Id.CompareTo(cursorId) > 0);
        }

        var items = await accounts
            .OrderBy(x => x.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;
        if (hasMore)
        {
            items = items.Take(limit).ToList();
        }

        var page = new PageResponse<AccountResponse>(
            items.Select(ToResponse).ToList(),
            hasMore ? items[^1].Id.ToString() : null);

        return Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var account = await db.NetworkAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return account is null ? Results.NotFound() : Results.Ok(ToResponse(account));
    }

    private static async Task<IResult> ChangePasswordAsync(
        Guid id,
        ChangePasswordRequest request,
        IdentityDbContext db,
        IPasswordHasher hasher,
        SessionClient sessions,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var account = await db.NetworkAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (account is null)
        {
            return Results.NotFound();
        }

        var validation = PasswordPolicy.Validate(request.NewPassword);
        if (validation.IsFailure)
        {
            return Results.Problem(validation.Reason.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        var now = DateTimeOffset.UtcNow;
        var credentials = await db.AccountCredentials
            .Where(x => x.AccountId == id)
            .ToListAsync(cancellationToken);

        var current = credentials.FirstOrDefault(x => x.Type == CredentialType.Password);
        if (current is not null)
        {
            if (hasher.Verify(request.NewPassword!, current.Secret))
            {
                return Results.Problem(
                    "the new password must differ from the current one",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            credentials.Remove(current);
            db.AccountCredentials.Remove(current);
        }

        var credentialResult = AccountCredential.Create(
            id, CredentialType.Password, hasher.Hash(request.NewPassword!), Pbkdf2PasswordHasher.Algorithm, now);

        if (credentialResult.IsFailure)
        {
            return Results.Problem(credentialResult.Reason.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        db.AccountCredentials.Add(credentialResult.Value);
        await db.SaveChangesAsync(cancellationToken);

        // A rotated password invalidates every existing session.
        await sessions.RevokeAllAsync(id.ToString(), "account.password-changed", cancellationToken);

        await audit.AppendAsync(
            "account.password-changed", "admin", null, "account", id.ToString(), "success",
            http.TraceIdentifier, null, cancellationToken);

        return Results.Ok(new { accountId = id });
    }

    private static Task<IResult> DisableAsync(
        Guid id,
        IdentityDbContext db,
        SessionClient sessions,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken) =>
        TransitionAsync(id, db, sessions, audit, http, "account.disabled", static (account, now) => account.Disable(now), cancellationToken);

    private static Task<IResult> LockAsync(
        Guid id,
        IdentityDbContext db,
        SessionClient sessions,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken) =>
        TransitionAsync(id, db, sessions, audit, http, "account.locked", static (account, now) => account.Lock(now), cancellationToken);

    private static async Task<IResult> TransitionAsync(
        Guid id,
        IdentityDbContext db,
        SessionClient sessions,
        AuditClient audit,
        HttpContext http,
        string action,
        Func<NetworkAccount, DateTimeOffset, Result> transition,
        CancellationToken cancellationToken)
    {
        var account = await db.NetworkAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (account is null)
        {
            return Results.NotFound();
        }

        var result = transition(account, DateTimeOffset.UtcNow);
        if (result.IsFailure)
        {
            return Results.Problem(result.Reason.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await db.SaveChangesAsync(cancellationToken);

        // Disabling an account must also kill its live sessions; otherwise a
        // compromised session survives until it expires.
        await sessions.RevokeAllAsync(account.Id.ToString(), action, cancellationToken);

        await audit.AppendAsync(
            action, "admin", null, "account", account.Id.ToString(), "success", http.TraceIdentifier, null, cancellationToken);

        return Results.Ok(ToResponse(account));
    }

    private static AccountResponse ToResponse(NetworkAccount account) => new(
        account.Id.ToString(),
        account.LegalEntityId?.ToString(),
        account.Realm,
        account.Username,
        account.Email,
        account.DisplayName,
        account.Status.ToString(),
        account.Type.ToString(),
        account.CreatedAt);
}
