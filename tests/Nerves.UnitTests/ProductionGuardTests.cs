using Microsoft.Extensions.Hosting;
using Nerves.ServiceDefaults;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class ProductionGuardTests
{
    [Fact]
    public void Development_may_seed()
    {
        Should.NotThrow(() => ProductionGuards.RejectSeedOutsideDevelopment(
            Env(Environments.Development), enabled: true, allowInProduction: false, "DevSeed"));
    }

    [Fact]
    public void Production_rejects_seeding_without_opt_in()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            ProductionGuards.RejectSeedOutsideDevelopment(
                Env(Environments.Production), enabled: true, allowInProduction: false, "DevSeed"));

        exception.Message.ShouldContain("DevSeed");
    }

    [Fact]
    public void An_explicit_opt_in_is_respected()
    {
        Should.NotThrow(() => ProductionGuards.RejectSeedOutsideDevelopment(
            Env(Environments.Production), enabled: true, allowInProduction: true, "DevSeed"));
    }

    [Fact]
    public void A_disabled_seed_never_throws()
    {
        Should.NotThrow(() => ProductionGuards.RejectSeedOutsideDevelopment(
            Env(Environments.Production), enabled: false, allowInProduction: false, "DevSeed"));
    }

    private static TestEnvironment Env(string name) => new(name);

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
