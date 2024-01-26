using Nerves.Dashboard.Services;
using Nerves.Dashboard.Services.Auth;

namespace Nerves.Dashboard;

public static class Instances
{
    public readonly static string BaseUrl = "http://localhost:5252/";

    public static AuthenticationManager? authenticationManager;

    public static NetworkRequest? networkRequest;

    public static void Init()
    {
        authenticationManager = new();

        networkRequest = new();

        Console.WriteLine($"@Init: {nameof(Instances)}");
        Console.WriteLine();
    }
}