using Nerves.ComplianceCn.Domain;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class ErasureRequestTests
{
    [Fact]
    public void Starts_pending_without_a_completion_time()
    {
        var request = ErasureRequest.Create("subject-1", "user request", DateTimeOffset.UtcNow);

        request.Status.ShouldBe("pending");
        request.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public void Completing_sets_status_and_timestamp()
    {
        var request = ErasureRequest.Create("subject-1", "user request", DateTimeOffset.UtcNow);

        request.Complete(DateTimeOffset.UtcNow);

        request.Status.ShouldBe("completed");
        request.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Failing_is_distinguished_from_completion()
    {
        var request = ErasureRequest.Create("subject-1", "user request", DateTimeOffset.UtcNow);

        request.Fail(DateTimeOffset.UtcNow);

        request.Status.ShouldBe("failed");
        request.CompletedAt.ShouldNotBeNull();
    }
}
