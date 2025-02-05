using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace RankUtils;

public class PluginConfig : IBasePluginConfig
{
    [JsonPropertyName("Debug")]
    public bool Debug { get; set; } = false;
    
    [JsonPropertyName("CronSettings")]
    public List<Cron> CronSettings { get; set; } =
    [
        new()
        {
            CronExpression = "0 0 1 */3 *", // Раз в 3 месяца
            Command = "css_lr_reset_ranks exp"
        }
    ];
    
    [JsonPropertyName("CacheSaveBanRank")]
    public int CacheSaveBanRank { get; set; } = 3;
    
    public class Cron
    {
        [JsonPropertyName("CronExpression")]
        public string CronExpression { get; set; } = "";
    
        [JsonPropertyName("Command")]
        public string Command { get; set; } = "";
    }

    public int Version { get; set; } = 1;
}