using Newtonsoft.Json;

namespace LiveBot.Json;

public class Activities
{
    [JsonProperty("activity_id")]
    public string ActivityId { get; private set; }

    [JsonProperty("points")]
    public int Points { get; private set; }

    [JsonProperty("score")]
    public int Score { get; private set; }

    [JsonProperty("rank")]
    public int Rank { get; private set; }
}