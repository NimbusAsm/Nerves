using Nerves.Identity.Security;
using Shouldly;

namespace Nerves.Identity.UnitTests;

public sealed class RecoveryCodeTests
{
    private readonly RecoveryCodes _recovery = new();

    [Fact]
    public void Generates_ten_unique_grouped_codes()
    {
        var codes = _recovery.Generate();

        codes.Count.ShouldBe(RecoveryCodes.Count);
        codes.Distinct().Count().ShouldBe(codes.Count);
        foreach (var code in codes)
        {
            code.Length.ShouldBe(9);
            code[4].ShouldBe('-');
        }
    }

    [Fact]
    public void Normalizes_case_and_separators()
    {
        RecoveryCodes.Normalize("abcd-efgh").ShouldBe("ABCDEFGH");
        RecoveryCodes.Normalize(" ABCD EFGH ").ShouldBe("ABCDEFGH");
    }

    [Fact]
    public void Hashes_are_stable_and_distinct()
    {
        var first = RecoveryCodes.HashCode("abcd-efgh");
        var second = RecoveryCodes.HashCode("ABCD EFGH");
        var other = RecoveryCodes.HashCode("ABCD-EFGI");

        first.ShouldBe(second);
        first.ShouldNotBe(other);
        first.Length.ShouldBe(64);
    }
}
