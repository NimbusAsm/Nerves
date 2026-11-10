using System.Text.Json;
using Nerves.Idp.Integration;

namespace Nerves.Idp.Federation;

/// <summary>Writes one audit event per directory sync pass.</summary>
public sealed class DirectorySyncAudit(AuditClient audit) : IDirectorySyncAudit
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task CompletedAsync(DirectorySyncSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await audit.AppendAsync(
            "directory.sync.completed",
            "system",
            null,
            "ldap",
            snapshot.LastError is null ? "success" : "failure",
            JsonSerializer.Serialize(new
            {
                scanned = snapshot.Scanned,
                created = snapshot.Created,
                updated = snapshot.Updated,
                unchanged = snapshot.Unchanged,
                skipped = snapshot.Skipped,
                failed = snapshot.Failed,
                error = snapshot.LastError,
            }, JsonOptions),
            cancellationToken);
    }
}
