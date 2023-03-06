using Newtonsoft.Json;

namespace LiveBot.Json;

public class TierEntries
{
    [JsonProperty("points")]
    public ulong Points { get; set; }

    [JsonProperty("rank")]
    public ulong Rank { get; set; }
}