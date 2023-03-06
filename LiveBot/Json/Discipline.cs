using Newtonsoft.Json;

namespace LiveBot.Json;

public class Discipline
{
    [JsonProperty("id")]
    public ulong Id { get; private set; }

    [JsonProperty("text_id")]
    public string TextId { get; private set; }

    [JsonProperty("family")]
    public ulong FamilyId { get; private set; }

    [JsonProperty("img_path")]
    public string ImgPath { get; private set; }
}