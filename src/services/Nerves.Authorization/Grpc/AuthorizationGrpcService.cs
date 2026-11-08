using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Nerves.Authorization.Domain;
using Nerves.Authorization.Persistence;
using Nerves.Contracts.Authorization.V1;

namespace Nerves.Authorization.Grpc;

/// <summary>Answers authorization questions over gRPC.</summary>
public sealed class AuthorizationGrpcService(AuthorizationDbContext db) : AuthorizationService.AuthorizationServiceBase
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

    private async Task<(List<string> Permissions, long Version)> ResolveAsync(
        string accountId,
        string legalEntityId,
        CancellationToken cancellationToken)
    {
        var subjects = new List<Guid>();
        if (Guid.TryParse(accountId, out var account)) subjects.Add(account);
        if (Guid.TryParse(legalEntityId, out var entity)) subjects.Add(entity);

        // Group roles apply to every member.
        if (account != Guid.Empty)
        {
            var groupIds = await db.GroupMembers
                .Where(x => x.AccountId == account)
                .Select(x => x.GroupId)
                .ToListAsync(cancellationToken);

            subjects.AddRange(groupIds);
        }

        if (subjects.Count == 0)
        {
            return (new List<string>(), 0);
        }

        var roleIds = await db.RoleAssignments
            .Where(x => subjects.Contains(x.SubjectId))
            .Select(x => x.RoleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var permissions = await (
                from rp in db.Set<RolePermission>()
                join p in db.Permissions on rp.PermissionId equals p.Id
                where roleIds.Contains(rp.RoleId)
                select p.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var version = await db.SubjectVersions
            .Where(x => subjects.Contains(x.SubjectId))
            .Select(x => (long?)x.Version)
            .MaxAsync(cancellationToken) ?? 0;

        return (permissions, version);
    }
}
