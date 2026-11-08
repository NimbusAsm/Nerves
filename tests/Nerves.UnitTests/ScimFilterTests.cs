using Nerves.Identity.Scim;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class ScimFilterTests
{
    [Fact]
    public void Parses_a_quoted_equality_filter()
    {
        ScimFilter.TryParse("userName eq \"alice\"", out var filter, out var error).ShouldBeTrue();

        error.ShouldBeNull();
        filter.Attribute.ShouldBe("userName");
        filter.Value.ShouldBe("alice");
    }

    [Fact]
    public void Parses_a_filter_with_spaces_inside_the_value()
    {
        ScimFilter.TryParse("displayName eq \"SCIM Team 1\"", out var filter, out _).ShouldBeTrue();

        filter.Value.ShouldBe("SCIM Team 1");
    }

    [Fact]
    public void Accepts_an_unquoted_value()
    {
        ScimFilter.TryParse("externalId eq abc-123", out var filter, out _).ShouldBeTrue();

        filter.Value.ShouldBe("abc-123");
    }

    [Fact]
    public void Rejects_unsupported_expressions()
    {
        ScimFilter.TryParse("userName sw \"al\"", out _, out var error).ShouldBeFalse();
        error.ShouldNotBeNull();

        ScimFilter.TryParse("userName", out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void Leaves_no_filter_when_the_input_is_empty()
    {
        ScimFilter.TryParse(null, out _, out var error).ShouldBeTrue();
        error.ShouldBeNull();
    }
}
