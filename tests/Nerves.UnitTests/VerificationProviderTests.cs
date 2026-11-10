using Microsoft.Extensions.Options;
using Nerves.Identity.Verification;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class VerificationProviderTests
{
    private static VerificationProviderFactory Factory(string configured, params IVerificationProvider[] providers) =>
        new(providers, Options.Create(new VerificationOptions { Provider = configured }));

    [Fact]
    public async Task Manual_provider_stays_pending_for_a_human_decision()
    {
        var provider = new ManualVerificationProvider();

        var response = await provider.StartAsync(
            new VerificationProviderRequest(
                Guid.NewGuid(), "LegalPerson", "UnifiedSocialCreditCode", new string('a', 64), "CN",
                "Acme", "https://nerves.test/callback", DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);

        provider.Name.ShouldBe("manual");
        response.Outcome.ShouldBe(VerificationProviderOutcome.AwaitingManualReview);
        response.Reference.ShouldBeNull();
    }

    [Fact]
    public void The_factory_resolves_the_configured_provider_and_explicit_names()
    {
        var factory = Factory("manual", new ManualVerificationProvider(), new HttpVerificationProviderStub());

        factory.Resolve(null)!.Name.ShouldBe("manual");
        factory.Resolve("HTTP")!.Name.ShouldBe("http");
        factory.Resolve("nope").ShouldBeNull();
    }

    [Fact]
    public void An_unknown_configured_provider_resolves_to_nothing()
    {
        Factory("bogus", new ManualVerificationProvider()).Resolve(null).ShouldBeNull();
    }

    [Theory]
    [InlineData("manual", null, true)]
    [InlineData("http", null, false)]
    [InlineData("http", "https://provider.test", true)]
    [InlineData("bogus", "https://provider.test", false)]
    public void Options_validation_matches_the_provider(string provider, string? publicBaseUrl, bool expected)
    {
        var options = new VerificationOptions { Provider = provider, PublicBaseUrl = publicBaseUrl };

        options.Validate().IsSuccess.ShouldBe(expected);
    }

    [Fact]
    public void The_attempt_lifetime_must_stay_within_bounds()
    {
        new VerificationOptions { AttemptLifetimeMinutes = 1 }.Validate().IsFailure.ShouldBeTrue();
        new VerificationOptions { AttemptLifetimeMinutes = 43_201 }.Validate().IsFailure.ShouldBeTrue();
        new VerificationOptions { AttemptLifetimeMinutes = 1440 }.Validate().IsSuccess.ShouldBeTrue();
    }

    private sealed class HttpVerificationProviderStub : IVerificationProvider
    {
        public string Name => "http";

        public Task<VerificationProviderResponse> StartAsync(
            VerificationProviderRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new VerificationProviderResponse(VerificationProviderOutcome.Processing, "ref", null));
    }
}
