using Newtonsoft.Json;

namespace LiveBot.Json;

public sealed class TheCrewHubJson
{
    public sealed class TheCrewHub
    {
        [JsonProperty("missions")] public Mission[] Missions { get; private set; }

        [JsonProperty("skills")] public Skill[] Skills { get; private set; }

        [JsonProperty("brands")] public Brand[] Brands { get; private set; }

        [JsonProperty("models")] public Model[] Models { get; private set; }

        [JsonProperty("disciplines")] public Discipline[] Disciplines { get; private set; }

        [JsonProperty("families")] public Family[] Families { get; private set; }

        [JsonProperty("news")] public News[] HubNews { get; private set; }
    }

    //Fame board
    public sealed class Fame
    {
        [JsonProperty("best")] public FameEntity Best { get; private set; }

        [JsonProperty("is_increasing")] public bool IsIncreasing { get; private set; }

        [JsonProperty("scores")] public FameEntity[] Scores { get; private set; }
    }

    public sealed class FameEntity
    {
        [JsonProperty("profile_id")] public string ProfileId { get; private set; }

        [JsonProperty("score")] public int Score { get; private set; }

        [JsonProperty("rank")] public int Rank { get; private set; }
    }

    public sealed class News
    {
        [JsonProperty("newsId")] public string Id { get; private set; }

        [JsonProperty("type")] public string Type { get; private set; }

        [JsonProperty("placement")] public string Placement { get; private set; }

        [JsonProperty("priority")] public int Priority { get; private set; }

        [JsonProperty("displayTime")] public int DisplayTime { get; private set; }

        [JsonProperty("publicationDate")] public DateTime? PublicationDate { get; private set; }

        [JsonProperty("expirationDate")] public DateTime? ExpirationDate { get; private set; }

        [JsonProperty("title")] public string Title { get; private set; }

        [JsonProperty("body")] public string Body { get; private set; }

        [JsonProperty("mediaURL")] public string MediaUrl { get; private set; }

        [JsonProperty("mediaType")] public string MediaType { get; private set; }

        [JsonProperty("profileId")] public string ProfileId { get; private set; }

        [JsonProperty("obj")] public NewsObj Obj { get; private set; }

        [JsonProperty("links")] public NewsLinks[] NewsItemLinks { get; private set; }
    }

    public sealed class NewsObj
    {
        [JsonProperty("tag")] public string Tag { get; private set; }
    }

    public sealed class NewsLinks
    {
        [JsonProperty("type")] public string Type { get; private set; }

        [JsonProperty("param")] public string Param { get; private set; }

        [JsonProperty("actionName")] public string ActionName { get; private set; }
    }
}