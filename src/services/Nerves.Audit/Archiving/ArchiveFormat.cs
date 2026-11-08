using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nerves.Audit.Domain;

namespace Nerves.Audit.Archiving;

/// <summary>Pure formatting helpers shared by the writer and its tests.</summary>
public static class ArchiveFormat
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string FileName(DateTimeOffset occurredAt) =>
        "events-" + occurredAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".ndjson";

    public static string ManifestName(string fileName) => fileName + ".sha256";

    public static string Line(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return JsonSerializer.Serialize(auditEvent, JsonOptions);
    }

    public static string Sha256Hex(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    public static string ManifestLine(string fileName, string content) =>
        Sha256Hex(content) + "  " + fileName + "\n";
}
