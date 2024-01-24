using Nerves.ApiServer.Services;
using Nerves.Data.MongoDB;

namespace Nerves.ApiServer;

public static class Instances
{
    public static ConfigManager? configManager;

    public static DataBaseConnector? dataBaseConnector;

    public static UserManager? userManager;

    public static void Init()
    {
        configManager = new ConfigManager()
            .SetLocation(".Nerves/Configs")
            .Load(ex => Console.WriteLine(ex.StackTrace))
            ;

        dataBaseConnector = new DataBaseConnector(configManager.ServerConfig.ConnectionString!);

        userManager = new UserManager(dataBaseConnector);

        Console.WriteLine($"@Init: {nameof(Instances)}");
        Console.WriteLine();
    }
}