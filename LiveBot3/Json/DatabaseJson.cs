using Newtonsoft.Json;

namespace LiveBot.Json;

public class DatabaseJson
{
    [JsonProperty("host")]
    public string Host { get; set; }

    [JsonProperty("username")]
    public string Username { get; set; }

    [JsonProperty("password")]
    public string Password { get; set; }

    [JsonProperty("database")]
    public string Database { get; set; }

    [JsonProperty("port")]
    public string Port { get; set; }
}