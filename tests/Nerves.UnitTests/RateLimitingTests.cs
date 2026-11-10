using Microsoft.Extensions.Configuration;
using Nerves.ServiceDefaults;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class RateLimitingTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void The_default_limit_is_registered()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        services.AddNervesRateLimiting(Config(new()));

        services.ShouldNotBeEmpty();
    }

    [Fact]
    public void A_non_positive_limit_is_rejected()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        Should.Throw<InvalidOperationException>(() =>
            services.AddNervesRateLimiting(Config(new() { ["RateLimiting:PermitLimit"] = "0" })));
    }
}
