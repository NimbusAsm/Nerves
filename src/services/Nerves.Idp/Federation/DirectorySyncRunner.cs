using Microsoft.Extensions.Options;
using Nerves.Idp.Options;

namespace Nerves.Idp.Federation;

/// <summary>Audit sink used by the sync pass; the production implementation talks gRPC.</summary>
public interface IDirectorySyncAudit
{
    Task CompletedAsync(DirectorySyncSnapshot snapshot, CancellationToken cancellationToken);
}

/// <summary>
/// One directory-to-identity reconciliation pass. Kept free of hosting concerns
/// so the mapping and idempotency rules can be unit tested with fakes.
/// </summary>
public sealed class DirectorySyncRunner(
    IDirectorySearch search,
    IDirectoryAccountWriter writer,
    IOptions<DirectorySyncOptions> syncOptions,
    IOptions<LdapOptions> ldapOptions,
    IDirectorySyncAudit audit) : IDirectorySyncRunner
{
    public async Task<DirectorySyncSnapshot> RunOnceAsync(CancellationToken cancellationToken)
    {
        var options = syncOptions.Value;
        var ldap = ldapOptions.Value;
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            var attributeMap = options.AttributeMap;
            var attributes = new[] { attributeMap.Username, attributeMap.Email, attributeMap.DisplayName }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var entries = await search.ListAsync(
                options.ResolveBaseDn(ldap.BaseDn),
                options.ResolveFilter(),
                attributes,
                cancellationToken);

            var counters = new SyncCounters();

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var record = DirectoryEntryMapper.Map(entry, options.Realm, attributeMap);
                if (record is null)
                {
                    counters.Skipped++;
                    continue;
                }

                var result = await writer.UpsertAsync(record, cancellationToken);
                if (!string.IsNullOrEmpty(result.FailureCode))
                {
                    counters.Failed++;
                    continue;
                }

                counters.Scanned++;
                if (result.Created)
                {
                    counters.Created++;
                }
                else if (result.Updated)
                {
                    counters.Updated++;
                }
                else
                {
                    counters.Unchanged++;
                }
            }

            var snapshot = new DirectorySyncSnapshot(
                startedAt,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow - startedAt,
                false,
                null,
                counters.Scanned,
                counters.Created,
                counters.Updated,
                counters.Unchanged,
                counters.Skipped,
                counters.Failed);

            await audit.CompletedAsync(snapshot, cancellationToken);
            return snapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return DirectorySyncSnapshot.FailedRun(startedAt, exception.GetType().Name);
        }
    }

    private sealed class SyncCounters
    {
        public int Scanned { get; set; }

        public int Created { get; set; }

        public int Updated { get; set; }

        public int Unchanged { get; set; }

        public int Skipped { get; set; }

        public int Failed { get; set; }
    }
}

/// <summary>Contract used by the hosted worker and by tests.</summary>
public interface IDirectorySyncRunner
{
    Task<DirectorySyncSnapshot> RunOnceAsync(CancellationToken cancellationToken);
}
