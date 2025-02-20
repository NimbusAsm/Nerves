using Nerves.ApiServer.Utils;
using Nerves.Data.MongoDB;
using Nerves.Shared.Options.DataBaseOptions;

namespace Nerves.ApiServer;

public static class Instances
{
    public static IConfigurationRoot? configuration;

    public static DataBaseConnector? dataBaseConnector;

    public static UserManager? userManager;

    public static void Init()
    {
        InitConfiguration();

        InitDataBaseConnector();

        InitUserManager();

        Console.WriteLine($"@Init: {nameof(Instances)}");
        Console.WriteLine();
    }

    public static void InitConfiguration()
    {
#if DEBUG
        var configFileName = "appsettings.Development.json";
#else
        var configFileName = "appsettings.json";
#endif

        configuration = new ConfigurationBuilder()
            .AddJsonFile(configFileName)
            .AddEnvironmentVariables()
            .Build()
            ;

        Console.WriteLine($"@Init: Server Configuration -> {nameof(configuration)}");
    }

    public static void InitDataBaseConnector()
    {
        var overConnectionString = configuration!["nerves_connectstr"];

        dataBaseConnector = new DataBaseConnector(
            overConnectionString ?? configuration!["Server:DataBase:ConnectionString"]!,
            configuration!["Server:DataBase:DataBaseName"]!
        );
    }

    public static void InitUserManager()
    {
        userManager = new UserManager(dataBaseConnector!);

        userManager.InsertUserAsync(UserUtil.GetDefaultAdmin(), new()
        {
            ActionWhenExists = AlreadyExistsActions.Skip
        });
    }
}
