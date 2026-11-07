using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Nerves.Contracts.Directory.V1;
using Nerves.Directory.Persistence;

namespace Nerves.Directory.Grpc;

/// <summary>Serves organization membership queries used by other services.</summary>
public sealed class DirectoryGrpcService(DirectoryDbContext db) : DirectoryService.DirectoryServiceBase
{
    public override Task<GetServiceInfoResponse> GetServiceInfo(
        GetServiceInfoRequest request,
        ServerCallContext context) =>
        Task.FromResult(new GetServiceInfoResponse
        {
            Name = "nerves-directory",
            Version = typeof(DirectoryGrpcService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            ContractVersion = "v1",
        });

    public override async Task<ListMembershipsResponse> ListMemberships(
        ListMembershipsRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
        {
            return new ListMembershipsResponse();
        }

        var rows = await (
                from membership in db.Memberships.AsNoTracking()
                join organization in db.Organizations.AsNoTracking()
                    on membership.OrganizationId equals organization.Id
                where membership.AccountId == accountId
                select new { organization.Id, organization.Name, membership.Role, membership.Status })
            .ToListAsync(context.CancellationToken);

        var response = new ListMembershipsResponse();
        response.Memberships.AddRange(rows.Select(r => new MembershipInfo
        {
            OrganizationId = r.Id.ToString(),
            OrganizationName = r.Name,
            Role = r.Role,
            Status = r.Status.ToString(),
        }));

        return response;
    }

    public override async Task<IsMemberResponse> IsMember(IsMemberRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrganizationId, out var organizationId)
            || !Guid.TryParse(request.AccountId, out var accountId))
        {
            return new IsMemberResponse { Member = false };
        }

        var membership = await db.Memberships.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId && x.AccountId == accountId,
                context.CancellationToken);

        return new IsMemberResponse
        {
            Member = membership is not null && membership.Status == Domain.MembershipStatus.Active,
            Role = membership?.Role ?? string.Empty,
        };
    }
}
