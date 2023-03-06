using Newtonsoft.Json;

namespace LiveBot.Json;

public class SummitLeaderboard
{
    [JsonProperty("entries")]
    public SummitLeaderboardEntries[] Entries { get; private set; }

    [JsonProperty("score_format")]
    public string ScoreFormat { get; private set; }
}
public class SummitLeaderboardEntries
{
    [JsonProperty("profile_id")]
    public string ProfileId { get; private set; }

    [JsonProperty("rank")]
    public int Rank { get; private set; }

    [JsonProperty("points")]
    public int Points { get; private set; }

    [JsonProperty("score")]
    public int Score { get; private set; }

    [JsonProperty("formatted_score")]
    public string FormattedScore { get; private set; }

    [JsonProperty("vehicle_id")]
    public ulong VehicleId { get; private set; }

    [JsonProperty("Vehicle_Level")]
    public int VehicleLevel { get; private set; }
}