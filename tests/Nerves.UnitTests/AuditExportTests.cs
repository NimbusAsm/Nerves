using Nerves.Audit.Api;
using Nerves.Audit.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class AuditExportTests
{
    [Fact]
    public void Escapes_commas_quotes_and_newlines()
    {
        AuditExport.Escape("plain").ShouldBe("plain");
        AuditExport.Escape("a,b").ShouldBe("\"a,b\"");
        AuditExport.Escape("say \"hi\"").ShouldBe("\"say \"\"hi\"\"\"");
        AuditExport.Escape("line1\nline2").ShouldBe("\"line1\nline2\"");
        AuditExport.Escape(null).ShouldBeEmpty();
    }

    [Fact]
    public void Writes_a_header_and_one_row_per_event()
    {
        var first = AuditEvent.Append(
            "nerves", 1, new string('0', 64), DateTimeOffset.UtcNow,
            "account", "a,b", "auth.login", null, null, "success", null, null, null);
        var second = AuditEvent.Append(
            "nerves", 2, first.Hash, DateTimeOffset.UtcNow,
            "account", "c", "auth.logout", null, null, "success", null, null, null);

        var csv = AuditExport.ToCsv([first, second]);
        var lines = csv.TrimEnd().Split('\n');

        lines.Length.ShouldBe(3);
        lines[0].ShouldStartWith("id,stream,sequence,occurredAt");
        lines[1].ShouldContain("\"a,b\"");
    }

    [Fact]
    public void File_names_carry_a_timestamp_and_format()
    {
        var now = new DateTimeOffset(2026, 11, 10, 8, 30, 15, TimeSpan.Zero);

        AuditExport.FileName("csv", now).ShouldBe("nerves-audit-20261110-083015.csv");
        AuditExport.FileName("json", now).ShouldBe("nerves-audit-20261110-083015.json");
    }
}
