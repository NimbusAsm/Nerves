using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Nerves.Cli.Infrastructure;

/// <summary>
/// Minimal <see cref="IHostEnvironment"/>. The seeders only use it for the
/// production guard, so the CLI supplies the environment name instead of
/// standing up a whole generic host.
/// </summary>
public sealed class CliHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "nerves";

    public string ContentRootPath { get; set; } = System.IO.Directory.GetCurrentDirectory();

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
