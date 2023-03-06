using Newtonsoft.Json;

namespace LiveBot.Json;

public class Model
{
    [JsonProperty("id")]
    public ulong Id { get; private set; }

    [JsonProperty("text_id")]
    public string TextId { get; private set; }

    [JsonProperty("vcat")]
    public string VCat { get; private set; }

    [JsonProperty("brand")]
    public string BrandId { get; private set; }
}