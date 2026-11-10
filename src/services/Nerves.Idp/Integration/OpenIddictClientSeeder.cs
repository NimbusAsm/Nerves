using Microsoft.Extensions.Options;
using Nerves.Idp.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Nerves.Idp.Integration;

/// <summary>
/// Registers the clients described by configuration. Both are public clients
/// using authorization code + PKCE; the login UI lives in the client.
/// </summary>
public sealed class OpenIddictClientSeeder(
    IServiceProvider services,
    IOptions<OpenIddictSeedOptions> options,
    Microsoft.Extensions.Hosting.IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var seed = options.Value;
        if (!seed.Enabled)
        {
            return;
        }

        Nerves.ServiceDefaults.ProductionGuards.RejectSeedOutsideDevelopment(
            environment, seed.Enabled, seed.AllowInProduction, "OpenIddictSeed");

        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        await EnsureClientAsync(
            manager,
            seed.ClientId,
            seed.DisplayName,
            seed.RedirectUris,
            seed.PostLogoutRedirectUris,
            cancellationToken);

        await EnsureClientAsync(
            manager,
            seed.AdminClientId,
            seed.AdminDisplayName,
            seed.AdminRedirectUris,
            seed.AdminPostLogoutRedirectUris,
            cancellationToken);
    }

    private static async Task EnsureClientAsync(
        IOpenIddictApplicationManager manager,
        string clientId,
        string displayName,
        string[] redirectUris,
        string[] postLogoutRedirectUris,
        CancellationToken cancellationToken)
    {
        var existing = await manager.FindByClientIdAsync(clientId, cancellationToken);

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = ClientTypes.Public,
            DisplayName = displayName,
            ConsentType = ConsentTypes.Implicit,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
                Permissions.Prefixes.Scope + "nerves.api",
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };

        foreach (var uri in redirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri));
        }

        foreach (var uri in postLogoutRedirectUris)
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        }

        if (existing is null)
        {
            await manager.CreateAsync(descriptor, cancellationToken);
            return;
        }

        // Keep redirect URIs in sync so configuration changes take effect on restart.
        await manager.PopulateAsync(descriptor, existing, cancellationToken);
        descriptor.RedirectUris.Clear();
        descriptor.PostLogoutRedirectUris.Clear();

        foreach (var uri in redirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri));
        }

        foreach (var uri in postLogoutRedirectUris)
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        }

        await manager.UpdateAsync(existing, descriptor, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
