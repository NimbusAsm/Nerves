using Nerves.ApiServer.Configs;

namespace Nerves.ApiServer.Services;

/// <summary>
/// 配置文件管理器
/// </summary>
public class ConfigManager
{
    /// <summary>
    /// 配置文件管理器信息类
    /// </summary>
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

    private Dictionary<string, ConfigBase>? _configs;

    internal ConfigManagerInfo? Infos;

    /// <summary>
    /// 配置文件管理器的构造函数
    /// </summary>
    public ConfigManager()
    {
        Infos = new();
        _configs = new();
    }

    /// <summary>
    /// 加载配置文件
    /// </summary>
    /// <returns>配置文件管理器自身</returns>
    public ConfigManager SetLocation(string location)
    {
        if (Infos is not null)
            Infos.Location = location;
        return this;
    }

    /// <summary>
    /// 加载配置文件
    /// </summary>
    /// <param name="failed">如果失败执行操作</param>
    /// <returns>配置文件管理器自身</returns>
    public ConfigManager Load(Action<Exception>? failed = null)
    {
        try
        {
            _configs?.Add(nameof(ServerConfig), new ServerConfig());

        }
        catch (Exception ex)
        {
            failed?.Invoke(ex);
        }

        return this;
    }
}
