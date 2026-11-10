using Nerves.Identity.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class VerificationAttemptTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_attempt_is_pending_with_a_normalized_provider()
    {
        var attempt = VerificationAttempt.Create(Guid.NewGuid(), "  Manual ", null, Now).Value;

        attempt.Status.ShouldBe(VerificationAttemptStatus.Pending);
        attempt.Provider.ShouldBe("manual");
        attempt.IsPending.ShouldBeTrue();
    }

    [Fact]
    public void Completing_an_attempt_records_the_outcome_once()
    {
        var attempt = VerificationAttempt.Create(Guid.NewGuid(), "http", "ref-1", Now).Value;

        attempt.Complete(VerificationAttemptStatus.Verified, "ok", Now.AddMinutes(1)).IsSuccess.ShouldBeTrue();
        attempt.Status.ShouldBe(VerificationAttemptStatus.Verified);
        attempt.CompletedAt.ShouldBe(Now.AddMinutes(1));

        // Idempotent for the same outcome, conflicting for a different one.
        attempt.Complete(VerificationAttemptStatus.Verified, "ok", Now.AddMinutes(2)).IsSuccess.ShouldBeTrue();
        var flipped = attempt.Complete(VerificationAttemptStatus.Rejected, "no", Now.AddMinutes(3));
        flipped.IsFailure.ShouldBeTrue();
        attempt.Status.ShouldBe(VerificationAttemptStatus.Verified);
    }

    [Fact]
    public void A_pending_status_is_not_a_valid_completion()
    {
        var attempt = VerificationAttempt.Create(Guid.NewGuid(), "manual", null, Now).Value;

        attempt.Complete(VerificationAttemptStatus.Pending, null, Now).IsFailure.ShouldBeTrue();
        attempt.Complete((VerificationAttemptStatus)99, null, Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Attaching_a_reference_keeps_the_existing_one_when_empty()
    {
        var attempt = VerificationAttempt.Create(Guid.NewGuid(), "http", "ref-1", Now).Value;

        attempt.AttachReference("   ", Now.AddMinutes(1)).IsSuccess.ShouldBeTrue();
        attempt.Reference.ShouldBe("ref-1");

        attempt.AttachReference("ref-2", Now.AddMinutes(2)).IsSuccess.ShouldBeTrue();
        attempt.Reference.ShouldBe("ref-2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_provider_is_rejected(string? provider)
    {
        VerificationAttempt.Create(Guid.NewGuid(), provider, null, Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Empty_legal_entity_id_is_rejected()
    {
        VerificationAttempt.Create(Guid.Empty, "manual", null, Now).IsFailure.ShouldBeTrue();
    }
}
