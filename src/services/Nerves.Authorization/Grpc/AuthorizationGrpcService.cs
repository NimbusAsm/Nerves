using Grpc.Core;
using Nerves.Authorization.Caching;
using Nerves.Authorization.Policy;
using Nerves.Contracts.Authorization.V1;

namespace Nerves.Authorization.Grpc;

/// <summary>Answers authorization questions over gRPC.</summary>
public sealed class AuthorizationGrpcService(CachedPermissions permissions, PdpClient pdp) : AuthorizationService.AuthorizationServiceBase
{
    public override Task<GetServiceInfoResponse> GetServiceInfo(
        GetServiceInfoRequest request,
        ServerCallContext context) =>
        Task.FromResult(new GetServiceInfoResponse
        {
            Name = "nerves-authorization",
            Version = typeof(AuthorizationGrpcService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            ContractVersion = "v1",
        });

    public override async Task<ListPermissionsResponse> ListPermissions(
        ListPermissionsRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (permissions, version) = await ResolveAsync(
            request.AccountId,
            request.LegalEntityId,
            context.CancellationToken);

        var response = new ListPermissionsResponse { AuthorizationVersion = version };
        response.Permissions.AddRange(permissions);
        return response;
    }

    public override async Task<CheckResponse> Check(CheckRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (permissions, version) = await ResolveAsync(
            request.AccountId,
            request.LegalEntityId,
            context.CancellationToken);

        var local = permissions.Any(
            p => string.Equals(p, request.Permission, StringComparison.OrdinalIgnoreCase));

        if (pdp.Enabled)
        {
            var decision = await pdp.DecideAsync(
                request.AccountId,
                request.LegalEntityId,
                request.Permission,
                context.CancellationToken);

            if (decision is null)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "policy decision point unavailable"));
            }

            return new CheckResponse { Allowed = decision.Allowed, AuthorizationVersion = version };
        }

        return new CheckResponse { Allowed = local, AuthorizationVersion = version };
    }

    private Task<(IReadOnlyList<string> Permissions, long Version)> ResolveAsync(
        string accountId,
        string legalEntityId,
        CancellationToken cancellationToken) =>
        permissions.ResolveAsync(accountId, legalEntityId, cancellationToken);
}
