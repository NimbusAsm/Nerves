using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Nerves.Contracts.Identity.V1;
using Nerves.Identity.Domain;
using Nerves.Identity.Persistence;
using Nerves.Identity.Security;

namespace Nerves.Identity.Grpc;

/// <summary>Serves the internal identity contract over gRPC.</summary>
public sealed class IdentityGrpcService(
    IdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IIdentifierHasher identifierHasher) : IdentityService.IdentityServiceBase
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

        var entry = await dbContext.RoutingIndex
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdentifierHash == hash, context.CancellationToken);

        if (entry is null)
        {
            // Constant-ish cost: still run one hash so existence cannot be timed.
            _ = passwordHasher.Verify(request.Password, passwordHasher.Hash("dummy"));
            return new VerifyCredentialResponse { Verified = false, FailureCode = "invalid_credentials" };
        }

        var credential = await dbContext.AccountCredentials
            .FirstOrDefaultAsync(
                x => x.AccountId == entry.AccountId && x.Type == CredentialType.Password,
                context.CancellationToken);

        if (credential is null || !passwordHasher.Verify(request.Password, credential.Secret))
        {
            return new VerifyCredentialResponse { Verified = false, FailureCode = "invalid_credentials" };
        }

        var account = await dbContext.NetworkAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == entry.AccountId, context.CancellationToken);

        if (account is null || account.Status != AccountStatus.Active)
        {
            return new VerifyCredentialResponse { Verified = false, FailureCode = "account_unavailable" };
        }

        credential.MarkUsed(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(context.CancellationToken);

        return new VerifyCredentialResponse
        {
            Verified = true,
            AccountId = account.Id.ToString(),
            LegalEntityId = account.LegalEntityId?.ToString() ?? string.Empty,
            DisplayName = account.DisplayName ?? account.Username,
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

        var account = await dbContext.NetworkAccounts
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
        };
    }

    public override Task<ListAccountPermissionsResponse> ListAccountPermissions(
        ListAccountPermissionsRequest request,
        ServerCallContext context)
    {
        // Authorization data lands in the authorization service (PLAN §5.3);
        // until then the identity service reports an empty, versioned snapshot.
        return Task.FromResult(new ListAccountPermissionsResponse { AuthorizationVersion = 0 });
    }
}
