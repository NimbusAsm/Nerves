using Nerves.ApiServer.Configs;

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
                if (value is null) throw new ArgumentNullException(nameof(value));

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
        _configs = new();
    }

    public ConfigManager SetLocation(string location)
    {
        if (Infos is not null)
            Infos.Location = location;

        return this;
    }

    private void LoadConfigFile<T>() where T : ConfigBase, new()
    {
        var name = nameof(T);

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
}
