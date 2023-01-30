using Newtonsoft.Json;

namespace LiveBot.Json;

public class Skill
{
    [JsonProperty("id")]
    public ulong Id { get; private set; }

    [JsonProperty("text_id")]
    public string TextId { get; private set; }

    [JsonProperty("family")]
    public string Family { get; private set; }

    [JsonProperty("type_text_id")]
    public string TypeTextId { get; private set; }

    [JsonProperty("score_type")]
    public string ScoreType { get; private set; }

    [JsonProperty("img_path")]
    public string ImgPath { get; private set; }
}