using Newtonsoft.Json;

namespace LiveBot.Json;

internal abstract class ConfigJson
    {
        public struct Config
        {
            [JsonProperty(nameof(LiveBot))]
            public Bot LiveBot { get; private set; }

            [JsonProperty(nameof(DevBot))]
            public Bot DevBot { get; private set; }

            [JsonProperty(nameof(DataBase))]
            public DataBase DataBase { get; private set; }

            [JsonProperty(nameof(Tce))]
            public TheCrewExchange Tce { get; private set; }

            [JsonProperty(nameof(TcHub))]
            public TheCrewHubApi TcHub { get; private set; }
        }

        public struct Bot
        {
            [JsonProperty("token")]
            public string Token { get; private set; }

            [JsonProperty("prefix")]
            public string CommandPrefix { get; private set; }
        }

        public struct DataBase
        {
            [JsonProperty("host")]
            public string Host { get; private set; }

            [JsonProperty("username")]
            public string Username { get; private set; }

            [JsonProperty("password")]
            public string Password { get; private set; }

            [JsonProperty("database")]
            public string Database { get; private set; }

            [JsonProperty("port")]
            public string Port { get; private set; }
        }

        public struct TheCrewExchange
        {
            [JsonProperty("key")]
            public string Key { get; private set; }

            [JsonProperty("link")]
            public string Link { get; private set; }
        }

        public struct TheCrewHubApi
        {
            [JsonProperty("summit")]
            public string Summit { get; private set; }

            [JsonProperty("gamedata")]
            public string GameData { get; private set; }
            [JsonProperty("dictionary")]
            public Dictionary<string,string> Locales { get; set; }

            [JsonProperty("news")]
            public string News { get; private set; }
        }
    }