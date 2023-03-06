using Newtonsoft.Json;

namespace LiveBot.Json;

public class Family
{
    [JsonProperty("id")]
    public ulong Id { get; private set; }

    [JsonProperty("text_id")]
    public string TextId { get; private set; }
}