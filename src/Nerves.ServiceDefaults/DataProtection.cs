using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Nerves.ServiceDefaults;

/// <summary>
/// Registers data protection with a persistent key ring. Secrets such as TOTP
/// seeds and passkey material are encrypted with these keys, so a container
/// restart without persistence would render them unreadable.
/// </summary>
public static class DataProtection
{
    public static IServiceCollection AddNervesDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var builder = services.AddDataProtection().SetApplicationName("nerves");
        var directory = configuration["DataProtection:KeysDirectory"];

        if (!string.IsNullOrWhiteSpace(directory))
        {
            builder.PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(directory)));
            return services;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "DataProtection:KeysDirectory must be configured outside Development; " +
                "without it, MFA secrets cannot be decrypted after a restart.");
        }

        return services;
    }
}
