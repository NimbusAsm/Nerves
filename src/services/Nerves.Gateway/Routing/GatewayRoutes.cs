using Yarp.ReverseProxy.Configuration;

namespace Nerves.Gateway.Routing;

/// <summary>
/// Route table lives in code so every environment proxies the same paths; only
/// the destination addresses vary, supplied through Gateway:Services:*.
/// </summary>
public static class GatewayRoutes
{
    private static readonly (string Id, int Order, string Cluster, string Path)[] Definitions =
    [
        ("sessions-by-account", 1, "session", "/api/v1/accounts/{accountId}/sessions"),
        ("sessions", 2, "session", "/api/v1/sessions/{**catch-all}"),
        ("roles", 3, "authorization", "/api/v1/roles/{**catch-all}"),
        ("permissions", 4, "authorization", "/api/v1/permissions/{**catch-all}"),
        ("audit", 5, "audit", "/api/v1/audit-events/{**catch-all}"),
        ("stats", 6, "identity", "/api/v1/stats"),
        ("accounts", 7, "identity", "/api/v1/accounts/{**catch-all}"),
        ("legal-entities", 8, "identity", "/api/v1/legal-entities/{**catch-all}"),
        ("organizations", 9, "directory", "/api/v1/organizations/{**catch-all}"),
        ("invitations", 10, "directory", "/api/v1/invitations/{**catch-all}"),
        ("compliance", 11, "compliance", "/api/v1/compliance/{**catch-all}"),
        ("notifications", 16, "notification", "/api/v1/notifications/{**catch-all}"),
        ("scim-users", 12, "identity", "/scim/v2/Users/{**catch-all}"),
        ("scim-groups", 13, "authorization", "/scim/v2/Groups/{**catch-all}"),
        ("idp-admin", 14, "idp", "/api/v1/admin/integrations/{**catch-all}"),
        ("authz-admin", 15, "authorization", "/api/v1/admin/pdp"),
    ];

    private static readonly Dictionary<string, string> DefaultAddresses = new(StringComparer.Ordinal)
    {
        ["identity"] = "http://127.0.0.1:5102/",
        ["session"] = "http://127.0.0.1:5103/",
        ["authorization"] = "http://127.0.0.1:5104/",
        ["audit"] = "http://127.0.0.1:5105/",
        ["directory"] = "http://127.0.0.1:5106/",
        ["compliance"] = "http://127.0.0.1:5107/",
        ["notification"] = "http://127.0.0.1:5108/",
        ["idp"] = "http://127.0.0.1:5101/",
    };

    public static (IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters) Build(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration.GetSection("Gateway:Services")
            .Get<Dictionary<string, string>>() ?? new Dictionary<string, string>(StringComparer.Ordinal);

        var addresses = new Dictionary<string, string>(DefaultAddresses, StringComparer.Ordinal);
        foreach (var (cluster, address) in configured)
        {
            if (!string.IsNullOrWhiteSpace(address))
            {
                addresses[cluster] = address;
            }
        }

        var routes = Definitions
            .Select(definition => new RouteConfig
            {
                RouteId = definition.Id,
                Order = definition.Order,
                ClusterId = definition.Cluster,
                Match = new RouteMatch { Path = definition.Path },
            })
            .ToList();

        var clusters = addresses
            .Select(pair => new ClusterConfig
            {
                ClusterId = pair.Key,
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.Ordinal)
                {
                    ["primary"] = new DestinationConfig { Address = pair.Value },
                },
            })
            .ToList();

        return (routes, clusters);
    }
}
