using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nerves.ServiceDefaults;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class DataProtectionTests
{
    private static ServiceCollection CreateServices() => new();

    private static IConfiguration Configuration(string? directory) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysDirectory"] = directory,
            })
            .Build();

    [Fact]
    public void Persists_to_the_configured_directory()
    {
        var services = CreateServices();
        var directory = Path.Combine(Path.GetTempPath(), "nerves-dp-test-" + Guid.NewGuid().ToString("N"));

        services.AddNervesDataProtection(Configuration(directory), new TestEnvironment(Environments.Production));

        using var provider = services.BuildServiceProvider();
        var dataProtection = provider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
        dataProtection.ShouldNotBeNull();
    }

    [Fact]
    public void Fails_outside_development_without_a_directory()
    {
        var services = CreateServices();

        Should.Throw<InvalidOperationException>(() =>
            services.AddNervesDataProtection(Configuration(null), new TestEnvironment(Environments.Production)));
    }

    [Fact]
    public void Development_may_use_the_default_ring()
    {
        var services = CreateServices();

        services.AddNervesDataProtection(Configuration(null), new TestEnvironment(Environments.Development));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>().ShouldNotBeNull();
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
