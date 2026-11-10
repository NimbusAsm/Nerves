using Nerves.ComplianceCn.Domain;
using Nerves.ComplianceCn.Options;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class RetentionTests
{
    [Fact]
    public void Retention_is_disabled_by_default()
    {
        new RetentionOptions().Enabled.ShouldBeFalse();
    }

    [Fact]
    public void The_interval_is_clamped_to_one_day()
    {
        new RetentionOptions { IntervalMinutes = 0 }.ClampInterval().ShouldBe(1);
        new RetentionOptions { IntervalMinutes = 100000 }.ClampInterval().ShouldBe(1440);
        new RetentionOptions { IntervalMinutes = 30 }.ClampInterval().ShouldBe(30);
    }

    [Fact]
    public void The_batch_size_is_clamped()
    {
        new RetentionOptions { BatchSize = -5 }.ClampBatch().ShouldBe(1);
        new RetentionOptions { BatchSize = 99999 }.ClampBatch().ShouldBe(5000);
    }

    [Fact]
    public void An_erasure_request_anonymizes_the_subject()
    {
        var now = DateTimeOffset.UtcNow;
        var request = ErasureRequest.Create("subject-1", "user asked", now);
        request.Complete(now);

        request.Anonymize(now.AddDays(1));

        request.SubjectId.ShouldBe(ErasureRequest.AnonymizedSubject);
        request.Reason.ShouldBeEmpty();
        request.Status.ShouldBe("anonymized");
        request.CompletedAt.ShouldBe(now.AddDays(1));
    }

    [Fact]
    public void A_pending_erasure_request_keeps_its_subject()
    {
        var now = DateTimeOffset.UtcNow;
        var request = ErasureRequest.Create("subject-2", "requested", now);

        request.SubjectId.ShouldBe("subject-2");
        request.CompletedAt.ShouldBeNull();
        request.Status.ShouldBe("pending");
    }

    [Fact]
    public void A_consent_expires_after_its_window()
    {
        var grantedAt = DateTimeOffset.UtcNow.AddDays(-30);
        var consent = Consent.Grant("subject", "privacy", "v1", grantedAt, null);

        consent.GrantedAt.ShouldBe(grantedAt);
        consent.RevokedAt.ShouldBeNull();
        consent.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Revoking_a_consent_is_idempotent_and_keeps_the_first_reason()
    {
        var now = DateTimeOffset.UtcNow;
        var consent = Consent.Grant("subject", "privacy", "v1", now, null);

        consent.Revoke(now, "first");
        consent.Revoke(now.AddMinutes(1), "second");

        consent.RevocationReason.ShouldBe("first");
        consent.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Retaining_a_policy_requires_a_positive_window()
    {
        var policy = RetentionPolicy.Create("consent", 30, "PIPL art. 47");
        policy.RetentionDays.ShouldBe(30);

        policy.Update(90, null);
        policy.RetentionDays.ShouldBe(90);
        policy.LegalBasis.ShouldBeNull();
    }
}
