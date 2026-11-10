using Nerves.Core;
using Nerves.Identity.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class LegalEntityTests
{
    private static LegalEntity CreateEntity(DateTimeOffset now) =>
        LegalEntity.Create(
            LegalEntityType.LegalPerson,
            "Example Ltd",
            RealNameId.Create(IdDocumentType.UnifiedSocialCreditCode, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), "CN").Value,
            now).Value;

    [Fact]
    public void A_new_entity_is_active()
    {
        var entity = CreateEntity(DateTimeOffset.UtcNow);

        entity.Status.ShouldBe(LegalEntityStatus.Active);
        entity.DisabledAt.ShouldBeNull();
    }

    [Fact]
    public void Disabling_records_the_timestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = CreateEntity(now);

        entity.Disable(now).IsSuccess.ShouldBeTrue();

        entity.Status.ShouldBe(LegalEntityStatus.Disabled);
        entity.DisabledAt.ShouldBe(now);
    }

    [Fact]
    public void Disabling_twice_is_a_conflict()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = CreateEntity(now);
        entity.Disable(now);

        entity.Disable(now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Enabling_restores_the_active_state()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = CreateEntity(now);
        entity.Disable(now);

        entity.Enable(now.AddMinutes(1)).IsSuccess.ShouldBeTrue();

        entity.Status.ShouldBe(LegalEntityStatus.Active);
        entity.DisabledAt.ShouldBeNull();
    }

    [Fact]
    public void Enabling_an_active_entity_is_a_conflict()
    {
        var entity = CreateEntity(DateTimeOffset.UtcNow);

        entity.Enable(DateTimeOffset.UtcNow).IsFailure.ShouldBeTrue();
    }
}
