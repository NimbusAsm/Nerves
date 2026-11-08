using Nerves.Authorization.Policy;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class PdpOptionsTests
{
    [Fact]
    public void Disabled_by_default()
    {
        var options = new PdpOptions();

        options.Url.ShouldBeNull();
        options.FailOpen.ShouldBeFalse();
        options.TimeoutSeconds.ShouldBe(3);
    }

    [Fact]
    public void Fail_closed_is_the_default()
    {
        var options = new PdpOptions { Url = "http://pdp:8080/decide" };

        options.FailOpen.ShouldBeFalse();
    }
}
