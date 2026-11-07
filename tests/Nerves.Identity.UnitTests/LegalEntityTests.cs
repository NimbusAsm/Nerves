using Nerves.Core;
using Nerves.Identity.Domain;
using Shouldly;

namespace Nerves.Identity.UnitTests;

public sealed class LegalEntityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 0, 0, 0, TimeSpan.Zero);
    private static readonly string ValidHash = new('a', 64);

    private static RealNameId RealName() =>
        RealNameId.Create(IdDocumentType.ResidentIdentityCard, ValidHash, "cn").Value;

    [Fact]
    public void Create_starts_pending()
    {
        var result = LegalEntity.Create(LegalEntityType.NaturalPerson, "Alice", RealName(), Now);

        result.IsSuccess.ShouldBeTrue();
        result.Value.VerificationState.ShouldBe(VerificationState.Pending);
        result.Value.DisplayName.ShouldBe("Alice");
        result.Value.CreatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_display_name(string? name)
    {
        var result = LegalEntity.Create(LegalEntityType.NaturalPerson, name, RealName(), Now);

        result.IsFailure.ShouldBeTrue();
        result.Reason.Code.ShouldBe("validation.failed");
    }

    [Fact]
    public void Verify_moves_pending_to_verified()
    {
        var entity = LegalEntity.Create(LegalEntityType.LegalPerson, "ACME", RealName(), Now).Value;

        var result = entity.MarkVerified("provider-1", Now);

        result.IsSuccess.ShouldBeTrue();
        entity.VerificationState.ShouldBe(VerificationState.Verified);
        entity.VerifiedAt.ShouldBe(Now);
        entity.ProviderReference.ShouldBe("provider-1");
    }

    [Fact]
    public void Verify_twice_is_conflict()
    {
        var entity = LegalEntity.Create(LegalEntityType.NaturalPerson, "Alice", RealName(), Now).Value;
        entity.MarkVerified("p", Now);

        var result = entity.MarkVerified("p", Now);

        result.IsFailure.ShouldBeTrue();
        result.Reason.Code.ShouldBe("conflict");
    }

    [Fact]
    public void Reject_only_allowed_while_pending()
    {
        var entity = LegalEntity.Create(LegalEntityType.NaturalPerson, "Alice", RealName(), Now).Value;
        entity.MarkVerified("p", Now);

        var result = entity.MarkRejected("p", Now);

        result.IsFailure.ShouldBeTrue();
        result.Reason.Code.ShouldBe("conflict");
    }

    [Fact]
    public void Expire_requires_verified_state()
    {
        var entity = LegalEntity.Create(LegalEntityType.NaturalPerson, "Alice", RealName(), Now).Value;

        entity.Expire(Now).IsFailure.ShouldBeTrue();

        entity.MarkVerified("p", Now);
        entity.Expire(Now).IsSuccess.ShouldBeTrue();
        entity.VerificationState.ShouldBe(VerificationState.Expired);
    }
}
