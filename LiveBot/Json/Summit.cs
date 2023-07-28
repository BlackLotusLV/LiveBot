using Newtonsoft.Json;

namespace LiveBot.Json;

public class Summit
{
    [JsonProperty("summit_id")]
    public ulong SummitId { get; private set; }

    [JsonProperty("text_id")]
    public string TextId { get; private set; }

    [JsonProperty("color1")]
    public string Color1 { get; private set; }

    [JsonProperty("color2")]
    public string Color2 { get; private set; }

    [JsonProperty("start_date")]
    public long StartDate { get; private set; }

    [JsonProperty("end_date")]
    public long EndDate { get; private set; }

    [JsonProperty("ticket_full")]
    public string CoverBig { get; private set; }

    [JsonProperty("ticket_short")]
    public string CoverSmall { get; private set; }

    [JsonProperty("id")]
    public long Id { get; private set; }

    [JsonProperty("events")]
    public Event[] Events { get; private set; }

    [JsonProperty("rewards")]
    public Reward[] Rewards { get; set; }
}
public class Event
{
    [JsonProperty("summit_id")]
    public string SummitId { get; private set; }

    [JsonProperty("is_mission")]
    public bool IsMission { get; private set; }

    [JsonProperty("id")]
    public ulong Id { get; private set; }

    [JsonProperty("constraint_text_id")]
    public string[] ConstraintTextId { get; set; }

    [JsonProperty("debug_constraint")]
    public string DebugConstraint { get; private set; }

    [JsonProperty("img_path")]
    public string ImgPath { get; private set; }

    [JsonProperty("modifiers")]
    public string[] Modifiers { get; private set; }

    public byte[] ImageByte { get; set; }
}

public class Reward
{
    [JsonProperty("summit_id")]
    public ulong SummitId { get; private set; }

    [JsonProperty("debug_name")]
    public string DebugName { get; private set; }

    [JsonProperty("level")]
    public int Level { get; private set; }

    [JsonProperty("title_text_id")]
    public string TitleTextId { get; private set; }

    [JsonProperty("debug_title")]
    public string DebugTitle { get; private set; }
    [JsonProperty("subtitle_text_id")]
    public string SubtitleTextId { get; set; }

    [JsonProperty("debug_subtitle")]
    public string DebugSubtitle { get; private set; }

    [JsonProperty("img_path")]
    public string ImgPath { get; private set; }

    [JsonProperty("type")]
    public string Type { get; private set; }

    [JsonProperty("extra")]
    public Dictionary<string, string> Extra { get; private set; }
}