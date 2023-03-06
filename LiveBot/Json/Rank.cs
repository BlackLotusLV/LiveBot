using Newtonsoft.Json;

namespace LiveBot.Json;

public class Rank
{
    [JsonProperty("points")]
    public ulong Points { get; private set; }

    [JsonProperty("rank")]
    public int UserRank { get; private set; }

    [JsonProperty("total_players")]
    public string PlayerCount { get; private set; }

    [JsonProperty("activities")]
    public Activities[] Activities { get; private set; }

    [JsonProperty("tier_entries")]
    public TierEntries[] TierEntries { get; private set; }
}