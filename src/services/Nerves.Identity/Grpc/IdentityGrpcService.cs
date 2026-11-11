using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Nerves.Contracts.Identity.V1;
using Nerves.Identity.Domain;
using Nerves.Identity.Persistence;
using Nerves.Identity.Security;

namespace Nerves.Identity.Grpc;

/// <summary>
/// Serves the internal identity contract over gRPC. Accounts live on shards, so
/// every account-scoped call resolves the routing index first and then opens the
/// shard that owns the account; identifier resolution still starts globally.
/// </summary>
public sealed partial class IdentityGrpcService(
    IdentityStore store,
    IPasswordHasher passwordHasher,
    PasskeyService passkeys,
    IIdentifierHasher identifierHasher,
    TotpService totp,
    TotpSecretProtector protector,
    Nerves.Identity.PasswordReset.PasswordResetService passwordReset,
    Microsoft.Extensions.Options.IOptions<Nerves.Identity.PasswordReset.PasswordResetOptions> passwordResetOptions) : IdentityService.IdentityServiceBase
{
    public override Task<GetServiceInfoResponse> GetServiceInfo(
        GetServiceInfoRequest request,
        ServerCallContext context) =>
        Task.FromResult(new GetServiceInfoResponse
        {
            Name = "nerves-identity",
            Version = typeof(IdentityGrpcService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            ContractVersion = "v1",
        });

    public override async Task<VerifyCredentialResponse> VerifyCredential(
        VerifyCredentialRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hash = identifierHasher.Hash("username", request.Realm, request.Identifier);

        RoutingIndexEntry? entry;
        await using (var global = store.Global())
        {
            entry = await global.RoutingIndex
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdentifierHash == hash, context.CancellationToken);
        }

        if (entry is null)
        {
            // Constant-ish cost: still run one hash so existence cannot be timed.
            _ = passwordHasher.Verify(request.Password, passwordHasher.Hash("dummy"));
            return new VerifyCredentialResponse { Verified = false, FailureCode = "invalid_credentials" };
        }

        await using var shard = store.AccountShard(entry.AccountId);

        var credential = await shard.AccountCredentials
            .FirstOrDefaultAsync(
                x => x.AccountId == entry.AccountId && x.Type == CredentialType.Password,
                context.CancellationToken);

        if (credential is null || !passwordHasher.Verify(request.Password, credential.Secret))
        {
            return new VerifyCredentialResponse { Verified = false, FailureCode = "invalid_credentials" };
        }

        var account = await shard.NetworkAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == entry.AccountId, context.CancellationToken);

        if (account is null || account.Status != AccountStatus.Active)
        {
            return new VerifyCredentialResponse { Verified = false, FailureCode = "account_unavailable" };
        }

        credential.MarkUsed(DateTimeOffset.UtcNow);
        await shard.SaveChangesAsync(context.CancellationToken);

        return new VerifyCredentialResponse
        {
            Verified = true,
            AccountId = account.Id.ToString(),
            LegalEntityId = account.LegalEntityId?.ToString() ?? string.Empty,
            DisplayName = account.DisplayName ?? account.Username,
            Email = account.Email ?? string.Empty,
        };
    }

    public override async Task<GetAccountResponse> GetAccount(
        GetAccountRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Guid.TryParse(request.AccountId, out var accountId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "account_id must be a UUID"));
        }

        await using var shard = store.AccountShard(accountId);
        var account = await shard.NetworkAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == accountId, context.CancellationToken);

        if (account is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "account not found"));
        }

        return new GetAccountResponse
        {
            AccountId = account.Id.ToString(),
            LegalEntityId = account.LegalEntityId?.ToString() ?? string.Empty,
            Realm = account.Realm,
            Username = account.Username,
            DisplayName = account.DisplayName ?? account.Username,
            Status = account.Status.ToString(),
            Shard = store.ShardFor(account.Id),
        };
    }

    public override async Task<HasActiveTotpResponse> HasActiveTotp(
        HasActiveTotpRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Guid.TryParse(request.AccountId, out var accountId))
        {
            return new HasActiveTotpResponse { Active = false };
        }

        await using var shard = store.AccountShard(accountId);
        var active = await shard.AccountCredentials
            .AnyAsync(
                x => x.AccountId == accountId && x.Type == CredentialType.Totp && x.ActivatedAt != null,
                context.CancellationToken);

        return new HasActiveTotpResponse { Active = active };
    }

    public override async Task<VerifyMfaResponse> VerifyMfa(
        VerifyMfaRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Guid.TryParse(request.AccountId, out var accountId) || string.IsNullOrWhiteSpace(request.Code))
        {
            return new VerifyMfaResponse { Valid = false };
        }

        var code = request.Code.Trim();
        await using var shard = store.AccountShard(accountId);

        // Six digits are treated as a TOTP code; anything else is a recovery code.
        if (code.Length == 6 && code.All(char.IsDigit))
        {
            var totpCredential = await shard.AccountCredentials.FirstOrDefaultAsync(
                x => x.AccountId == accountId && x.Type == CredentialType.Totp && x.ActivatedAt != null,
                context.CancellationToken);

            if (totpCredential is not null)
            {
                var secret = protector.TryUnprotect(totpCredential.Secret);
                if (secret is not null && totp.Verify(secret, code))
                {
                    totpCredential.MarkUsed(DateTimeOffset.UtcNow);
                    await shard.SaveChangesAsync(context.CancellationToken);
                    return new VerifyMfaResponse { Valid = true, Method = "totp" };
                }
            }
        }

        var hash = RecoveryCodes.HashCode(code);
        var recovery = await shard.AccountCredentials.FirstOrDefaultAsync(
            x => x.AccountId == accountId && x.Type == CredentialType.RecoveryCode && x.Secret == hash,
            context.CancellationToken);

        if (recovery is null)
        {
            return new VerifyMfaResponse { Valid = false };
        }

        // Recovery codes are single use: consume the row.
        shard.AccountCredentials.Remove(recovery);
        await shard.SaveChangesAsync(context.CancellationToken);

        return new VerifyMfaResponse { Valid = true, Method = "recovery" };
    }

    public override Task<ListAccountPermissionsResponse> ListAccountPermissions(
        ListAccountPermissionsRequest request,
        ServerCallContext context)
    {
        // Authorization data lands in the authorization service (PLAN §5.3);
        // until then the identity service reports an empty, versioned snapshot.
        return Task.FromResult(new ListAccountPermissionsResponse { AuthorizationVersion = 0 });
    }

    public override async Task<ResolveIdentifierResponse> ResolveIdentifier(
        ResolveIdentifierRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        var realm = string.IsNullOrWhiteSpace(request.Realm) ? "nerves" : request.Realm;
        var hash = identifierHasher.Hash(request.IdentifierType, realm, request.Value);

        RoutingIndexEntry? entry;
        await using (var global = store.Global())
        {
            entry = await global.RoutingIndex
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdentifierHash == hash, context.CancellationToken);
        }

        if (entry is null)
        {
            return new ResolveIdentifierResponse { Found = false };
        }

        await using var shard = store.AccountShard(entry.AccountId);
        var account = await shard.NetworkAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == entry.AccountId, context.CancellationToken);

        if (account is null)
        {
            return new ResolveIdentifierResponse { Found = false };
        }

        return new ResolveIdentifierResponse
        {
            Found = true,
            Shard = store.ShardFor(account.Id),
            AccountId = account.Id.ToString(),
            LegalEntityId = account.LegalEntityId?.ToString() ?? string.Empty,
            Realm = account.Realm,
            DisplayName = account.DisplayName,
            Email = account.Email ?? string.Empty,
        };
    }

    public override async Task<HasActivePasskeyResponse> HasActivePasskey(
        HasActivePasskeyRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Guid.TryParse(request.AccountId, out var accountId))
        {
            return new HasActivePasskeyResponse { Active = false };
        }

        await using var shard = store.AccountShard(accountId);
        var active = await shard.AccountCredentials.AnyAsync(
            x => x.AccountId == accountId && x.Type == CredentialType.Passkey && x.ActivatedAt != null,
            context.CancellationToken);

        return new HasActivePasskeyResponse { Active = active };
    }

    public override async Task<StartPasskeyAssertionResponse> StartPasskeyAssertion(
        StartPasskeyAssertionRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Guid.TryParse(request.AccountId, out var accountId))
        {
            return new StartPasskeyAssertionResponse();
        }

        var (options, challengeId) = await passkeys.StartAssertionAsync(accountId, context.CancellationToken);
        return new StartPasskeyAssertionResponse { OptionsJson = options, ChallengeId = challengeId };
    }

    public override async Task<CompletePasskeyAssertionResponse> CompletePasskeyAssertion(
        CompletePasskeyAssertionRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Guid.TryParse(request.AccountId, out var accountId))
        {
            return new CompletePasskeyAssertionResponse { Valid = false };
        }

        using var document = System.Text.Json.JsonDocument.Parse(request.AssertionJson);
        var (ok, _) = await passkeys.CompleteAssertionAsync(
            accountId, request.ChallengeId, document.RootElement, context.CancellationToken);

        return new CompletePasskeyAssertionResponse { Valid = ok };
    }

    public override async Task<DisableAccountResponse> DisableAccount(
        DisableAccountRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Guid.TryParse(request.AccountId, out var accountId))
        {
            return new DisableAccountResponse { Disabled = false };
        }

        await using var shard = store.AccountShard(accountId);
        var account = await shard.NetworkAccounts
            .FirstOrDefaultAsync(x => x.Id == accountId, context.CancellationToken);

        if (account is null)
        {
            return new DisableAccountResponse { Disabled = false };
        }

        var result = account.Disable(DateTimeOffset.UtcNow);
        if (result.IsFailure)
        {
            return new DisableAccountResponse { Disabled = false };
        }

        await shard.SaveChangesAsync(context.CancellationToken);
        return new DisableAccountResponse { Disabled = true };
    }

    public override async Task<UpsertDirectoryAccountResponse> UpsertDirectoryAccount(
        UpsertDirectoryAccountRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        var realm = string.IsNullOrWhiteSpace(request.Realm) ? "nerves" : request.Realm.Trim().ToLowerInvariant();
        var username = request.Username?.Trim();
        var email = DirectoryAccountPlanner.NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(username) || username.Length > 64)
        {
            return new UpsertDirectoryAccountResponse { FailureCode = "invalid_username" };
        }

        if (email is not null && email.Length > 320)
        {
            return new UpsertDirectoryAccountResponse { FailureCode = "invalid_email" };
        }

        // Accounts are sharded, so both idempotency keys are resolved by fan-out.
        var byUsername = await store.FindByUsernameAsync(realm, username, context.CancellationToken);
        var byEmail = email is null ? null : await store.FindByEmailAsync(email, context.CancellationToken);

        var candidateIds = new[] { byUsername?.Id, byEmail?.Id }
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .ToArray();

        var emailTaken = false;
        if (email is not null)
        {
            var owners = await store.QueryShardsAsync(async db => await db.NetworkAccounts
                .AsNoTracking()
                .Where(x => x.Email == email && !candidateIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(context.CancellationToken), context.CancellationToken);
            emailTaken = owners.Count > 0;
        }

        var plan = DirectoryAccountPlanner.Plan(byUsername, byEmail, email, request.DisplayName, emailTaken);

        var now = DateTimeOffset.UtcNow;

        if (plan.Action == DirectoryAccountAction.EmailConflict)
        {
            return new UpsertDirectoryAccountResponse
            {
                FailureCode = "email_conflict",
                AccountId = byUsername?.Id.ToString() ?? string.Empty,
            };
        }

        if (plan.Account is not null)
        {
            var update = plan.Account.ApplyDirectoryProfile(plan.Email, plan.DisplayName, now);
            if (update.IsFailure)
            {
                return new UpsertDirectoryAccountResponse
                {
                    FailureCode = "invalid_profile",
                    AccountId = plan.Account.Id.ToString(),
                };
            }

            if (plan.Action == DirectoryAccountAction.Update)
            {
                await using var shard = store.AccountShard(plan.Account.Id);
                shard.NetworkAccounts.Update(plan.Account);
                await shard.SaveChangesAsync(context.CancellationToken);
            }

            return new UpsertDirectoryAccountResponse
            {
                Created = false,
                Updated = plan.Action == DirectoryAccountAction.Update,
                AccountId = plan.Account.Id.ToString(),
            };
        }

        // A directory account is provisioned by an organisation, which is what
        // AccountType.Managed means. Such an account still needs a legal entity,
        // so provisioning creates a placeholder entity it can be anchored to.
        var placeholder = LegalEntity.Create(
            LegalEntityType.Other,
            $"{username} ({realm})",
            RealNameId.Create(
                IdDocumentType.Other,
                identifierHasher.Hash("directory", realm, request.DirectoryId.Length > 0 ? request.DirectoryId : username),
                "CN").Value,
            now);

        if (placeholder.IsFailure)
        {
            return new UpsertDirectoryAccountResponse { FailureCode = "invalid_directory_id" };
        }

        var created = NetworkAccount.Create(
            placeholder.Value.Id,
            realm,
            username,
            email,
            null,
            DirectoryAccountPlanner.Normalize(request.DisplayName),
            AccountType.Managed,
            now);

        if (created.IsFailure)
        {
            return new UpsertDirectoryAccountResponse { FailureCode = "invalid_account" };
        }

        var accountId = created.Value.Id;

        // The placeholder entity is global; the account and its indexes follow
        // the same two-phase write as the HTTP create path.
        await using (var global = store.Global())
        {
            global.LegalEntities.Add(placeholder.Value);
            await global.SaveChangesAsync(context.CancellationToken);
        }

        var indexes = new List<RoutingIndexEntry>
        {
            RoutingIndexEntry.Create(
                identifierHasher.Hash("username", realm, username),
                "username",
                realm,
                accountId,
                placeholder.Value.Id,
                store.ShardFor(accountId),
                now),
        };

        if (email is not null)
        {
            indexes.Add(RoutingIndexEntry.Create(
                identifierHasher.Hash("email", realm, email),
                "email",
                realm,
                accountId,
                placeholder.Value.Id,
                store.ShardFor(accountId),
                now));
        }

        var write = await store.CreateAccountAsync(created.Value, null, indexes, context.CancellationToken);
        if (write.IsFailure)
        {
            return new UpsertDirectoryAccountResponse { FailureCode = "conflict" };
        }

        return new UpsertDirectoryAccountResponse
        {
            Created = true,
            Updated = false,
            AccountId = accountId.ToString(),
        };
    }
}
