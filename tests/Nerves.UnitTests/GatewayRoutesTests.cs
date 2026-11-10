using Microsoft.Extensions.Configuration;
using Nerves.Gateway.Routing;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class GatewayRoutesTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Routes_cover_every_service_prefix()
    {
        var (routes, clusters) = GatewayRoutes.Build(Config([]));

        routes.ShouldContain(r => r.ClusterId == "identity");
        routes.ShouldContain(r => r.ClusterId == "session");
        routes.ShouldContain(r => r.ClusterId == "authorization");
        routes.ShouldContain(r => r.ClusterId == "audit");
        routes.ShouldContain(r => r.ClusterId == "directory");
        routes.ShouldContain(r => r.ClusterId == "compliance");
        routes.ShouldContain(r => r.ClusterId == "idp");
        routes.ShouldContain(r => r.ClusterId == "notification");
        clusters.Count.ShouldBe(8);
    }

    [Fact]
    public void Configured_addresses_override_the_defaults()
    {
        var (_, clusters) = GatewayRoutes.Build(Config(new()
        {
            ["Gateway:Services:identity"] = "http://identity.nerves.svc:80/",
            ["Gateway:Services:audit"] = "http://audit.nerves.svc:80/",
        }));

        clusters.Single(c => c.ClusterId == "identity")
            .Destinations!["primary"].Address.ShouldBe("http://identity.nerves.svc:80/");
        clusters.Single(c => c.ClusterId == "audit")
            .Destinations!["primary"].Address.ShouldBe("http://audit.nerves.svc:80/");
        clusters.Single(c => c.ClusterId == "session")
            .Destinations!["primary"].Address.ShouldBe("http://127.0.0.1:5103/");
    }
}
