using System.Globalization;
using System.Text;
using Nerves.Audit.Domain;

namespace Nerves.Audit.Api;

/// <summary>Formats audit events for download without pulling in a CSV library.</summary>
public static class AuditExport
{
    public static readonly string[] Columns =
    [
        "id",
        "stream",
        "sequence",
        "occurredAt",
        "actorType",
        "actorId",
        "action",
        "targetType",
        "targetId",
        "result",
        "hash",
    ];

    public static string FileName(string format, DateTimeOffset now) =>
        "nerves-audit-" + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "." + format;

    public static string ToCsv(IEnumerable<AuditEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', Columns));

        foreach (var auditEvent in events)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Escape(auditEvent.Id.ToString()),
                Escape(auditEvent.Stream),
                Escape(auditEvent.Sequence.ToString(CultureInfo.InvariantCulture)),
                Escape(auditEvent.OccurredAt.ToString("O", CultureInfo.InvariantCulture)),
                Escape(auditEvent.ActorType),
                Escape(auditEvent.ActorId),
                Escape(auditEvent.Action),
                Escape(auditEvent.TargetType),
                Escape(auditEvent.TargetId),
                Escape(auditEvent.Result),
                Escape(auditEvent.Hash),
            }));
        }

        return builder.ToString();
    }

    /// <summary>RFC 4180 style escaping: wrap in quotes and double embedded quotes.</summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            || value.Contains('\r', StringComparison.Ordinal))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}
