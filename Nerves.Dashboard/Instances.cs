namespace Nerves.Dashboard;

public static class Instances
{
    public static LoginStateManager? LoginStateManager { get; set; }

    public static void Init()
    {
        LoginStateManager = new();

        Console.WriteLine($"@Init: {nameof(Instances)}");
        Console.WriteLine();
    }
}