using Newtonsoft.Json;

namespace LiveBot.Json;

public class Brand
{
    [JsonProperty("id")]
    public string Id { get; private set; }

    [JsonProperty("text_id")]
    public string TextId { get; private set; }

    [JsonProperty("rank")]
    public int Rank { get; private set; }
}