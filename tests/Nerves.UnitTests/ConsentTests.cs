using Nerves.ComplianceCn.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class ConsentTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void A_fresh_consent_is_active()
    {
        var consent = Consent.Grant("subject", "privacy", "1", Now, null);

        consent.IsActive.ShouldBeTrue();
        consent.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public void An_expired_consent_is_not_active()
    {
        var consent = Consent.Grant("subject", "privacy", "1", Now.AddDays(-10), Now.AddDays(-1));

        consent.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Revocation_is_idempotent_and_keeps_the_first_reason()
    {
        var consent = Consent.Grant("subject", "privacy", "1", Now, null);

        consent.Revoke(Now, "user request");
        consent.Revoke(Now.AddHours(1), "second reason");

        consent.IsActive.ShouldBeFalse();
        consent.RevocationReason.ShouldBe("user request");
    }

    [Fact]
    public void Retention_policy_update_keeps_one_row_per_category()
    {
        var policy = RetentionPolicy.Create("audit-log", 180, null);
        var originalId = policy.Id;

        policy.Update(365, "CSL art.21");

        policy.Id.ShouldBe(originalId);
        policy.RetentionDays.ShouldBe(365);
        policy.LegalBasis.ShouldBe("CSL art.21");
    }
}
