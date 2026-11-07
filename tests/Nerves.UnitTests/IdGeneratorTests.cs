using Nerves.Core;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class IdGeneratorTests
{
    [Fact]
    public void Generates_uuid_version_7()
    {
        var id = IdGenerator.NewId();

        // RFC 9562: the version nibble lives in the high half of byte 6.
        ((id.ToByteArray(bigEndian: true)[6] >> 4) & 0xF).ShouldBe(7);
    }

    [Fact]
    public void Ids_generated_later_sort_after_earlier_ones()
    {
        var first = IdGenerator.NewId(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var second = IdGenerator.NewId(new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero));

        string.CompareOrdinal(first.ToString(), second.ToString()).ShouldBeLessThan(0);
    }
}
