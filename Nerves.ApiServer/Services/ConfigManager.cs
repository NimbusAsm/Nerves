using Nerves.Shared.Configs;

namespace Nerves.ApiServer.Services;

public class ConfigManager
{
    internal class ConfigManagerInfo
    {
        private string? _location;

        internal string? Location
        {
            get => _location;
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                _location = Path.GetFullPath(value);

                if (!Directory.Exists(_location))
                    Directory.CreateDirectory(_location);
            }
        }
    }

    private readonly Dictionary<string, ConfigBase>? _configs;

    internal ConfigManagerInfo? Infos;

    public ConfigManager()
    {
        Infos = new();

        _configs = [];
    }

    public ConfigManager SetLocation(string location)
    {
        if (Infos is not null)
            Infos.Location = location;

        return this;
    }

    private void LoadConfigFile<T>() where T : ConfigBase, new()
    {
        var name = typeof(T).Name;

        ArgumentNullException.ThrowIfNull(name, nameof(name));

        _configs?.Add(
            name,
            $"{Infos?.Location}/{name}.json".Load<T>()
        );
    }

    public ConfigManager Load(Action<Exception>? failed = null)
    {
        try
        {
            LoadConfigFile<ServerConfig>();
        }
        catch (Exception ex)
        {
            failed?.Invoke(ex);
        }

        return this;
    }

    public ServerConfig ServerConfig => _configs?["ServerConfig"] as ServerConfig ?? throw new Exception("Can not find ServerConfig.");
}
