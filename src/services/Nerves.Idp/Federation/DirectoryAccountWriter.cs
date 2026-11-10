using Grpc.Core;
using Nerves.Contracts.Identity.V1;

namespace Nerves.Idp.Federation;

/// <summary>Outcome of one identity upsert call.</summary>
public sealed record DirectoryUpsertResult(bool Created, bool Updated, string AccountId, string FailureCode);

/// <summary>Write side of the sync, replaceable in tests.</summary>
public interface IDirectoryAccountWriter
{
    Task<DirectoryUpsertResult> UpsertAsync(DirectoryAccountRecord record, CancellationToken cancellationToken);
}

/// <summary>Forwards the normalized record to the identity service over gRPC.</summary>
public sealed class DirectoryAccountWriter(IdentityService.IdentityServiceClient client) : IDirectoryAccountWriter
{
    public async Task<DirectoryUpsertResult> UpsertAsync(
        DirectoryAccountRecord record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            var reply = await client.UpsertDirectoryAccountAsync(
                new UpsertDirectoryAccountRequest
                {
                    Realm = record.Realm,
                    Username = record.Username,
                    Email = record.Email ?? string.Empty,
                    DisplayName = record.DisplayName ?? string.Empty,
                    DirectoryId = record.DirectoryId,
                },
                cancellationToken: cancellationToken);

            return new DirectoryUpsertResult(reply.Created, reply.Updated, reply.AccountId, reply.FailureCode);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.Unavailable)
        {
            throw;
        }
        catch (RpcException exception)
        {
            return new DirectoryUpsertResult(false, false, string.Empty, exception.StatusCode.ToString());
        }
    }
}
