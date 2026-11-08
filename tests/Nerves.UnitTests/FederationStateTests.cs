using Nerves.Idp.Federation;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class FederationStateTests
{
    [Fact]
    public void A_fresh_state_is_usable()
    {
        var state = FederationState.Create("upstream", "/admin", TimeSpan.FromMinutes(10));

        state.IsUsable(DateTimeOffset.UtcNow).ShouldBeTrue();
        state.Provider.ShouldBe("upstream");
        state.ReturnUrl.ShouldBe("/admin");
    }

    [Fact]
    public void Consuming_a_state_makes_it_unusable()
    {
        var state = FederationState.Create("upstream", null, TimeSpan.FromMinutes(10));

        state.Consume();

        state.IsUsable(DateTimeOffset.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void An_expired_state_is_not_usable()
    {
        var state = FederationState.Create("upstream", null, TimeSpan.FromMinutes(-1));

        state.IsUsable(DateTimeOffset.UtcNow).ShouldBeFalse();
    }
}
