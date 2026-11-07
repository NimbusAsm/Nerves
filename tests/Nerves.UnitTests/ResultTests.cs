using Nerves.Core;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class ResultTests
{
    [Fact]
    public void Success_exposes_value()
    {
        var result = Result.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldBe(42);
        result.Reason.ShouldBe(Reason.None);
    }

    [Fact]
    public void Failure_carries_error_and_hides_value()
    {
        var error = Reason.NotFound("account not found");

        var result = Result.Failure<int>(error);

        result.IsFailure.ShouldBeTrue();
        result.Reason.ShouldBe(error);
        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }

    [Theory]
    [InlineData("validation.failed")]
    [InlineData("conflict")]
    [InlineData("not_found")]
    [InlineData("forbidden")]
    public void Error_factories_use_stable_codes(string expectedCode)
    {
        var error = expectedCode switch
        {
            "validation.failed" => Reason.Validation("x"),
            "conflict" => Reason.Conflict("x"),
            "not_found" => Reason.NotFound("x"),
            "forbidden" => Reason.Forbidden("x"),
            _ => throw new ArgumentOutOfRangeException(nameof(expectedCode)),
        };

        error.Code.ShouldBe(expectedCode);
    }
}
