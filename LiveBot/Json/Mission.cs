using Newtonsoft.Json;

namespace LiveBot.Json;

public class Mission
{
    [JsonProperty("id")]
    public ulong Id { get; private set; }

    [JsonProperty("text_id")]
    public string TextId { get; private set; }

    [JsonProperty("type")]
    public string Type { get; private set; }

    [JsonProperty("unlock_level")]
    public int UnlockLevel { get; private set; }

    [JsonProperty("image_path")]
    public string ImgPath { get; private set; }

    [JsonProperty("discipline")]
    public ulong DisciplineId { get; private set; }
}