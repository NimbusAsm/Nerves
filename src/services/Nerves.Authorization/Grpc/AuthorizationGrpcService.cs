using Grpc.Core;
using Nerves.Authorization.Caching;
using Nerves.Contracts.Authorization.V1;

namespace Nerves.Authorization.Grpc;

/// <summary>Answers authorization questions over gRPC.</summary>
public sealed class AuthorizationGrpcService(CachedPermissions permissions) : AuthorizationService.AuthorizationServiceBase
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

        var allowed = permissions.Any(p => string.Equals(p, request.Permission, StringComparison.OrdinalIgnoreCase));

        return new CheckResponse { Allowed = allowed, AuthorizationVersion = version };
    }

    private Task<(IReadOnlyList<string> Permissions, long Version)> ResolveAsync(
        string accountId,
        string legalEntityId,
        CancellationToken cancellationToken) =>
        permissions.ResolveAsync(accountId, legalEntityId, cancellationToken);
}
