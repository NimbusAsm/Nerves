using Nerves.Audit.Archiving;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class ArchiveFormatTests
{
    [Fact]
    public void Names_files_by_utc_day()
    {
        var at = DateTimeOffset.Parse("2026-11-08T23:30:00+08:00", System.Globalization.CultureInfo.InvariantCulture);

        ArchiveFormat.FileName(at).ShouldBe("events-2026-11-08.ndjson");
        ArchiveFormat.ManifestName("events-2026-11-08.ndjson").ShouldBe("events-2026-11-08.ndjson.sha256");
    }

    [Fact]
    public void Hashes_content_with_sha256()
    {
        // Known vector: SHA-256 of "abc".
        ArchiveFormat.Sha256Hex("abc").ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void Manifest_line_matches_the_sha256sum_format()
    {
        var line = ArchiveFormat.ManifestLine("events-2026-11-08.ndjson", "abc");

        line.ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  events-2026-11-08.ndjson\n");
    }

    [Fact]
    public void Serializes_an_event_as_one_json_line()
    {
        var auditEvent = Nerves.Audit.Domain.AuditEvent.Append(
            "nerves", 1, new string('0', 64), DateTimeOffset.UtcNow,
            "account", "a", "auth.login", null, null, "success", null, null, null);

        var line = ArchiveFormat.Line(auditEvent);

        line.ShouldContain("\"stream\":\"nerves\"");
        line.ShouldContain("\"action\":\"auth.login\"");
        line.Contains('\n').ShouldBeFalse();
    }
}
