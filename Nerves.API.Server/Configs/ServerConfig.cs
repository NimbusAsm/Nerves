using System.Text.Json.Serialization;

namespace Nerves.API.Server.Configs
{
    /// <summary>
    /// 服务器配置信息
    /// </summary>
    public class ServerConfig : ConfigBase
    {
        /// <summary>
        /// 服务器名称
        /// </summary>
        [JsonInclude]
        public string? ServerName { get; set; } = "Nerves API Server";

        /// <summary>
        /// 服务器上次启动时间
        /// </summary>
        [JsonInclude]
        public DateTime? LastStartTime { get; set; } = DateTime.Now;
    }
}
