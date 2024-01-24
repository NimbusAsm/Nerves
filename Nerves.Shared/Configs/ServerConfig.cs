using System.Text.Json.Serialization;

namespace Nerves.Shared.Configs;

public class ServerConfig : ConfigBase
{
    [JsonInclude]
    public string? ServerName { get; set; } = "Nerves API Server";

    [JsonInclude]
    public DateTime? LastStartTime { get; set; } = DateTime.Now;

    [JsonInclude]
    public string? ConnectionString { get; set; } = "mongodb://localhost:27017/Nerves";
}
