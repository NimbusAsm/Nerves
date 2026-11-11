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
        IdentityStore store,
        IPasswordHasher hasher,
        IIdentifierHasher identifiers,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<AccountType>(request.LegalEntityId is null ? nameof(AccountType.Service) : nameof(AccountType.Personal), out var type))
        {
            return Results.Problem("Invalid account type.", statusCode: StatusCodes.Status400BadRequest);
        }

        await using var global = store.Global();
        if (!string.IsNullOrWhiteSpace(request.LegalEntityId?.ToString())
            && !await global.LegalEntities.AnyAsync(x => x.Id == request.LegalEntityId, cancellationToken))
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
            if (await global.RoutingIndex.AnyAsync(x => x.IdentifierHash == hash, cancellationToken))
            {
                return Results.Problem(
                    $"The {identifierType} is already in use.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            indexes.Add(RoutingIndexEntry.Create(
                hash, identifierType, request.Realm, account.Id, account.LegalEntityId, store.ShardFor(account.Id), now));
        }

        var created = await store.CreateAccountAsync(account, credentialResult.Value, indexes, cancellationToken);
        if (created.IsFailure)
        {
            return Results.Problem(created.Reason.Message, statusCode: StatusCodes.Status409Conflict);
        }

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
        IdentityStore store,
        int limit = 50,
        string? cursor = null,
        string? status = null,
        string? realm = null,
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        AccountStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<AccountStatus>(status, ignoreCase: true, out var candidate))
        {
            parsedStatus = candidate;
        }

        Guid? cursorId = Guid.TryParse(cursor, out var parsedCursor) ? parsedCursor : null;

        var items = await store.ListAccountsAsync(
            new AccountFilter(realm, parsedStatus, query),
            limit,
            cursorId,
            cancellationToken);

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
        IdentityStore store,
        CancellationToken cancellationToken)
    {
        await using var db = store.AccountShard(id);
        var account = await db.NetworkAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return account is null ? Results.NotFound() : Results.Ok(ToResponse(account));
    }

    private static async Task<IResult> ChangePasswordAsync(
        Guid id,
        ChangePasswordRequest request,
        IdentityStore store,
        IPasswordHasher hasher,
        SessionClient sessions,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var db = store.AccountShard(id);
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
        IdentityStore store,
        SessionClient sessions,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken) =>
        TransitionAsync(id, store, sessions, audit, http, "account.disabled", static (account, now) => account.Disable(now), cancellationToken);

    private static Task<IResult> LockAsync(
        Guid id,
        IdentityStore store,
        SessionClient sessions,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken) =>
        TransitionAsync(id, store, sessions, audit, http, "account.locked", static (account, now) => account.Lock(now), cancellationToken);

    private static async Task<IResult> TransitionAsync(
        Guid id,
        IdentityStore store,
        SessionClient sessions,
        AuditClient audit,
        HttpContext http,
        string action,
        Func<NetworkAccount, DateTimeOffset, Result> transition,
        CancellationToken cancellationToken)
    {
        await using var db = store.AccountShard(id);
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
