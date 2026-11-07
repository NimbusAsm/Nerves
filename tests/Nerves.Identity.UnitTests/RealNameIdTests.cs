using Nerves.Identity.Domain;
using Shouldly;

namespace Nerves.Identity.UnitTests;

public sealed class RealNameIdTests
{
    [Fact]
    public void Normalizes_hash_and_country()
    {
        var hash = new string('A', 64);

        var result = RealNameId.Create(IdDocumentType.ResidentIdentityCard, hash, "cn");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Hash.ShouldBe(hash.ToLowerInvariant());
        result.Value.Country.ShouldBe("CN");
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void Rejects_bad_hash(string hash)
    {
        RealNameId.Create(IdDocumentType.Passport, hash, "CN").IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("CHN")]
    public void Rejects_bad_country(string country)
    {
        RealNameId.Create(IdDocumentType.Passport, new string('a', 64), country).IsFailure.ShouldBeTrue();
    }
}
