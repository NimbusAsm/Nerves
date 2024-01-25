using Nerves.Dashboard.Services.Auth;

namespace Nerves.Dashboard;

public static class Instances
{
    public static AuthenticationManager? authenticationManager;

    public static void Init()
    {
        authenticationManager = new();

        Console.WriteLine($"@Init: {nameof(Instances)}");
        Console.WriteLine();
    }
}