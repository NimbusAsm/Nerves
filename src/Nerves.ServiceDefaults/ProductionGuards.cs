using Microsoft.Extensions.Hosting;

namespace Nerves.ServiceDefaults;

/// <summary>
/// Refuses development-only seeding outside Development unless the deployment
/// explicitly opts in. Prevents a default-password administrator from being
/// created in production by a copied configuration.
/// </summary>
public static class ProductionGuards
{
    public static void RejectSeedOutsideDevelopment(
        IHostEnvironment environment,
        bool enabled,
        bool allowInProduction,
        string seedName)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (!enabled || environment.IsDevelopment() || allowInProduction)
        {
            return;
        }

        throw new InvalidOperationException(
            seedName + " is enabled outside Development. Set " + seedName +
            ":AllowInProduction=true to override, or disable the seed.");
    }
}
