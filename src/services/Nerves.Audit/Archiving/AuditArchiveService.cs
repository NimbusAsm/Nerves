using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nerves.Audit.Persistence;

namespace Nerves.Audit.Archiving;

/// <summary>
/// Copies unarchived events into day-partitioned NDJSON files and records the
/// archive timestamp. Files are append-only; a sidecar manifest holds the
/// SHA-256 of the final content for later verification.
/// </summary>
public sealed class AuditArchiveService(AuditDbContext db, IOptions<ArchiveOptions> options)
{
    public sealed record ArchiveResult(int Events, IReadOnlyList<string> Files);

    public async Task<ArchiveResult> RunAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var events = await db.AuditEvents
            .Where(x => x.ArchivedAt == null)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.Sequence)
            .Take(settings.BatchSize)
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            return new ArchiveResult(0, []);
        }

        var directory = Path.GetFullPath(settings.Directory);
        Directory.CreateDirectory(directory);

        var files = new List<string>();
        var byDay = events.GroupBy(x => ArchiveFormat.FileName(x.OccurredAt));

        foreach (var group in byDay)
        {
            var path = Path.Combine(directory, group.Key);

            // Append keeps earlier archives intact; the manifest always covers
            // the complete file, so verification stays meaningful.
            await using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            await using (var writer = new StreamWriter(stream))
            {
                foreach (var auditEvent in group.OrderBy(x => x.Sequence))
                {
                    await writer.WriteAsync(ArchiveFormat.Line(auditEvent).AsMemory(), cancellationToken);
                    await writer.WriteLineAsync();
                }
            }

            var content = await File.ReadAllTextAsync(path, cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(directory, ArchiveFormat.ManifestName(group.Key)),
                ArchiveFormat.ManifestLine(group.Key, content),
                cancellationToken);

            files.Add(path);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var auditEvent in events)
        {
            auditEvent.MarkArchived(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ArchiveResult(events.Count, files);
    }
}
