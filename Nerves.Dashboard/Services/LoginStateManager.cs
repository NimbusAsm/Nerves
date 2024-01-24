namespace Nerves.Dashboard;

public class LoginStateManager
{
    public LoginStateManager()
    {
        Console.WriteLine($"@Init: {nameof(LoginStateManager)}");
    }

    public bool HasLogin { get; set; } = false;
}
