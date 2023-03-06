using Newtonsoft.Json;

namespace LiveBot.Json;

public struct Bot
{
    [JsonProperty("token")]
    public string Token { get; private set; }

    [JsonProperty("prefix")]
    public string CommandPrefix { get; private set; }
}