using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nerves.Core;
using Nerves.Identity.Domain;
using Nerves.Identity.Integration;
using Nerves.Identity.Persistence;
using Nerves.Identity.Security;

namespace Nerves.Identity.PasswordReset;

/// <summary>
/// Single implementation of the reset flow, shared by the IdP proxy and by the
/// internal gRPC contract. Accounts are sharded, so the routing index resolves
/// the account and every account-scoped read then targets its shard.
/// </summary>
public sealed class PasswordResetService(
    IdentityStore store,
    IIdentifierHasher identifiers,
    IPasswordHasher hasher,
    PasswordResetTokens tokens,
    ISessionRevoker sessions,
    INotificationSender notifications,
    AuditClient audit,
    IOptions<PasswordResetOptions> options,
    ILogger<PasswordResetService> logger)
{
    private static readonly Action<ILogger, Guid, Exception?> MailSkipped =
        LoggerMessage.Define<Guid>(LogLevel.Information, new EventId(1, "PasswordResetMailSkipped"),
            "password reset email could not be queued for account {AccountId}");

    /// <summary>
    /// Issues a token when the identifier resolves to an active account with an
    /// email address. Always returns success so callers stay uniform.
    /// </summary>
    public async Task<bool> RequestAsync(
        string? identifier,
        string? realm,
        string? resetUrlBase,
        string? traceId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 320)
        {
            return false;
        }

        var effectiveRealm = string.IsNullOrWhiteSpace(realm)
            ? options.Value.DefaultRealm
            : realm.Trim().ToLowerInvariant();

        var now = DateTimeOffset.UtcNow;
        var trimmed = identifier.Trim();

        RoutingIndexEntry? entry;
        await using (var global = store.Global())
        {
            // Users type their identifier however they remember it: exact form
            // first (usernames stay case sensitive), then the normalised forms.
            entry = null;
            foreach (var (type, value) in new[]
                     {
                         ("username", trimmed),
                         ("email", trimmed.ToLowerInvariant()),
                         ("phone", trimmed),
                         ("username", trimmed.ToLowerInvariant()),
                     })
            {
                if (entry is not null)
                {
                    break;
                }

                var candidate = identifiers.Hash(type, effectiveRealm, value);
                entry = await global.RoutingIndex
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.IdentifierHash == candidate, cancellationToken);
            }
        }

        if (entry is null)
        {
            await audit.AppendAsync(
                "auth.password-reset.requested", "anonymous", null, "account", null, "ignored",
                traceId, "{\"reason\":\"unknown_identifier\"}", cancellationToken);
            return false;
        }

        NetworkAccount? account;
        await using (var shard = store.AccountShard(entry.AccountId))
        {
            account = await shard.NetworkAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == entry.AccountId, cancellationToken);
        }

        if (account is null || account.Status != AccountStatus.Active || string.IsNullOrWhiteSpace(account.Email))
        {
            return false;
        }

        var raw = await tokens.IssueAsync(
            account.Id, now, TimeSpan.FromMinutes(options.Value.LifetimeMinutes), cancellationToken);

        var baseUrl = string.IsNullOrWhiteSpace(resetUrlBase)
            ? options.Value.PublicBaseUrl
            : resetUrlBase;

        var resetUrl = (string.IsNullOrWhiteSpace(baseUrl) ? "https://nerves.local" : baseUrl.TrimEnd('/'))
                       + "/reset-password?token=" + Uri.EscapeDataString(raw);

        var queued = await notifications.SendPasswordResetAsync(
            account.Email!,
            string.IsNullOrWhiteSpace(account.DisplayName) ? account.Username : account.DisplayName!,
            resetUrl,
            options.Value.LifetimeMinutes,
            cancellationToken);

        if (!queued)
        {
            MailSkipped(logger, account.Id, null);
        }

        await audit.AppendAsync(
            "auth.password-reset.requested", "anonymous", null, "account", account.Id.ToString(),
            "success", traceId, null, cancellationToken);

        return true;
    }

    /// <summary>
    /// Spends a token, replaces the password and revokes every session. Returns
    /// the account id on success so callers can audit it.
    /// </summary>
    public async Task<Result<Guid>> ConfirmAsync(
        string? rawToken,
        string? newPassword,
        string? traceId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 512)
        {
            return Result.Failure<Guid>(Reason.Validation("The reset token is invalid or has expired."));
        }

        var policy = PasswordPolicy.Validate(newPassword);
        if (policy.IsFailure)
        {
            return Result.Failure<Guid>(policy.Reason);
        }

        var now = DateTimeOffset.UtcNow;
        var token = await tokens.FindUsableAsync(rawToken, now, cancellationToken);
        if (token is null)
        {
            return Result.Failure<Guid>(Reason.Validation("The reset token is invalid or has expired."));
        }

        await using var db = store.AccountShard(token.AccountId);

        var account = await db.NetworkAccounts.FirstOrDefaultAsync(x => x.Id == token.AccountId, cancellationToken);
        if (account is null || account.Status != AccountStatus.Active)
        {
            return Result.Failure<Guid>(Reason.Validation("The reset token is invalid or has expired."));
        }

        var storedToken = await db.PasswordResetTokens
            .FirstOrDefaultAsync(x => x.Id == token.Id, cancellationToken);
        if (storedToken is null)
        {
            return Result.Failure<Guid>(Reason.Validation("The reset token is invalid or has expired."));
        }

        var consumed = storedToken.Consume(now);
        if (consumed.IsFailure)
        {
            return Result.Failure<Guid>(Reason.Validation("The reset token is invalid or has expired."));
        }

        var current = await db.AccountCredentials
            .FirstOrDefaultAsync(x => x.AccountId == account.Id && x.Type == CredentialType.Password, cancellationToken);

        if (current is not null)
        {
            if (hasher.Verify(newPassword!, current.Secret))
            {
                return Result.Failure<Guid>(Reason.Validation("the new password must differ from the current one"));
            }

            db.AccountCredentials.Remove(current);
        }

        var created = AccountCredential.Create(
            account.Id, CredentialType.Password, hasher.Hash(newPassword!), Pbkdf2PasswordHasher.Algorithm, now);

        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Reason);
        }

        db.AccountCredentials.Add(created.Value);
        await db.SaveChangesAsync(cancellationToken);

        // A reset must invalidate every session: an attacker holding a live
        // session loses it the moment the legitimate owner resets.
        await sessions.RevokeAllAsync(account.Id.ToString(), "account.password-reset", cancellationToken);

        await audit.AppendAsync(
            "account.password-reset", "anonymous", null, "account", account.Id.ToString(),
            "success", traceId, null, cancellationToken);

        return Result.Success(account.Id);
    }
}
