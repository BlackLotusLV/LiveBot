using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using LiveBot.DB;
using LiveBot.Json;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LiveBot.Services;

public interface ITheCrewHubService
{
    Summit[] Summit { get; }
    Mission[] Missions { get; }
    Skill[] Skills { get; }
    Model[] Models { get; }
    Brand[] Brands { get; }
    Discipline[] Disciplines { get; }
    Family[] Families { get; }
    byte[,][] RewardsImagesBytes { get; }
    Dictionary<string, Dictionary<string, string>> Locales { get; }
    Task GetSummitDataAsync(bool isForced = false);
    string DictionaryLookup(string id, string locale = "en-GB");
    Task GetGameDataAsync(bool isForced = false);
    FontCollection FontCollection { get; set; }

    Task StartServiceAsync(DiscordClient client);
    Task<(Image<Rgba32>,bool)> BuildEventImageAsync(Event summitEvent, Rank rank, DB.UbiInfo ubiInfo,User user, byte[] eventImageBytes, bool isCorner = false, bool isSpecial = false);
}
public class TheCrewHubService : ITheCrewHubService
{
    private readonly HttpClient _httpClient;
    private DiscordClient _client;
    public Summit[] Summit { get;private set; }
    public Mission[] Missions { get;private set; }
    public Skill[] Skills { get; private set; }
    public Model[] Models { get; private set; }
    public Brand[] Brands { get;private set; }
    public Discipline[] Disciplines { get;private set; }
    public Family[] Families { get;private set; }
    public byte[,][] RewardsImagesBytes { get; set; } = new byte[4, 4][];
    public Dictionary<string, Dictionary<string, string>> Locales { get; private set; } = new();
    public FontCollection FontCollection { get; set; } = new();
    

    public TheCrewHubService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task StartServiceAsync(DiscordClient client)
    {
        _client = client;
        await GetGameDataAsync();
        await GetSummitDataAsync();
        await LoadLocaleDataAsync();
        FontCollection.Add("Assets/Fonts/HurmeGeometricSans4-Black.ttf");
        FontCollection.Add("Assets/Fonts/Noto Sans Mono CJK JP Bold.otf");
        FontCollection.Add("Assets/Fonts/NotoSansArabic-Bold.ttf");

    }

    public async Task LoadLocaleDataAsync()
    {
        _client.Logger.LogInformation("Loading The Crew Hub locale data");
        using var sr = new StreamReader("ConfigFiles/TheCrewHub.json");
        JObject jsonObject = JObject.Parse(await sr.ReadToEndAsync());
        var locales = jsonObject["dictionary"]?.ToObject<Dictionary<string, string>>();
        if (locales==null)return;
        foreach (var locale in locales)
        {
            Locales.Add(locale.Key, JsonConvert.DeserializeObject<Dictionary<string,string>>(await _httpClient.GetStringAsync(locale.Value)));
        }
    }
    
    public async Task GetGameDataAsync(bool isForced = false)
    {
        _client.Logger.LogInformation("Loading the crew hub game data");
        string json;
        try
        {
            json = await _httpClient.GetStringAsync("https://api.thecrew-hub.com/v1/data/game?fields=missions,skills,brands,models,disciplines,families");
        }
        catch (WebException e)
        {
            _client.Logger.LogError(e,"Error while downloading game data from hub.");
            return;
        }

        JObject jsonObject = JObject.Parse(json);
        Skills = jsonObject["skills"]?.ToObject<Skill[]>();
        Models = jsonObject["models"]?.ToObject<Model[]>();
        Missions  = jsonObject["missions"]?.ToObject<Mission[]>();
        Brands  = jsonObject["brands"]?.ToObject<Brand[]>();
        Disciplines  = jsonObject["disciplines"]?.ToObject<Discipline[]>();
        Families  = jsonObject["families"]?.ToObject<Family[]>();
    }

    public async Task GetSummitDataAsync(bool isForced = false)
    {
        _client.Logger.LogInformation("Loading the crew hub summit data");
        string json;
        var oldSummit = Summit;
        try
        {
            json = await _httpClient.GetStringAsync("https://api.thecrew-hub.com/v1/data/summit");
        }
        catch (WebException e)
        {
            _client.Logger.LogError(e, "Error while getting summit data");
            return;
        }
        
        Summit = JsonConvert.DeserializeObject<Summit[]>(json);
        
        if (Summit == oldSummit && !isForced) return;
        await Parallel.ForEachAsync(Summit[0].Events, new ParallelOptions(),
            async (e, token) =>
            {
                Summit[0].Events.First(w => w.Id == e.Id).ImageByte = await _httpClient.GetByteArrayAsync($"https://www.thecrew-hub.com/gen/assets/summits/{e.ImgPath}", token);
            });

        for (var i = 0; i < 4; i++)
        {
            for (var j = 0; j < Summit[i].Rewards.Length; j++)
            {
                if (Summit[i].Rewards[j].ImgPath=="")
                {
                    RewardsImagesBytes[i, j] = null;
                }
                else
                {
                    RewardsImagesBytes[i, j] = await _httpClient.GetByteArrayAsync($"https://www.thecrew-hub.com/gen/assets/summits/{Summit[i].Rewards[j].ImgPath}");
                }
            }
        }
        _client.Logger.LogInformation("The Crew Hub information downloaded");
    }

    public string DictionaryLookup(string id, string locale = "en-GB")
    {
        if (locale == null || Locales.TryGetValue(locale, out Dictionary<string, string> dictionary))
        {
            dictionary = Locales.FirstOrDefault(w => w.Key == "en-GB").Value;
        }

        string hubText = dictionary.FirstOrDefault(w => w.Key.Equals(id)).Value ?? "[Item Name Missing]";
        return WebUtility.HtmlDecode(hubText);
    }
    
    public async Task<(Image<Rgba32>, bool)> BuildEventImageAsync(Event summitEvent, Rank rank, UbiInfo ubiInfo,User user, byte[] eventImageBytes, bool isCorner = false, bool isSpecial = false)
        {
            var locale = "en-GB";
            if (user!= null)
                locale = user.Locale;
            var eventImage = Image.Load<Rgba32>(eventImageBytes);
            Activities activity = null;
            if (rank != null)
            {
                activity = rank.Activities.FirstOrDefault(w => w.ActivityId.Equals(summitEvent.Id.ToString()));
            }
            if (summitEvent.IsMission && !isSpecial && !isCorner)
            {
                eventImage.Mutate(ctx => ctx
                .Resize(368, 239)
                    );
            }
            else if (isCorner)
            {
                eventImage.Mutate(ctx => ctx
                .Resize(380, 245)
                );
            }
            else if (isSpecial)
            {
                eventImage.Mutate(ctx => ctx
                    .Resize(380, 483)
                    );
            }
            Font basefont = new(FontCollection.Get("HurmeGeometricSans4 Black"), 18);
            Font summitCaps15 = new(FontCollection.Get("HurmeGeometricSans4 Black"), 15);
            Font vehicleFont = new(FontCollection.Get("HurmeGeometricSans4 Black"), 11.5f);
            if (activity == null)
            {
                Image<Rgba32> notComplete = new(eventImage.Width, eventImage.Height);
                TextOptions textOptions = new(basefont)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Origin = new PointF(notComplete.Width / 2, notComplete.Height / 2),
                    FallbackFontFamilies = new[] { FontCollection.Get("Noto Sans Mono CJK JP Bold"), FontCollection.Get("Noto Sans Arabic") }
                };
                notComplete.Mutate(ctx => ctx
                    .Fill(Color.Black)
                    .DrawText(textOptions, "Event not completed!", Color.White)
                    );
                eventImage.Mutate(ctx => ctx
                .DrawImage(notComplete, new Point(0, 0), 0.8f)
                );
                return (eventImage, false);
            }

            string thisEventNameId = summitEvent.IsMission ? Missions.Where(w => w.Id == summitEvent.Id).Select(s => s.TextId).FirstOrDefault() : Skills.Where(w => w.Id == summitEvent.Id).Select(s => s.TextId).FirstOrDefault();

            string eventLeaderboardString =
                await _httpClient.GetStringAsync($"https://api.thecrew-hub.com/v1/summit/{Summit[0].Id}/leaderboard/{ubiInfo.Platform}/{summitEvent.Id}{(ubiInfo.ProfileId!="0"?$"?profile={ubiInfo.ProfileId}":"")}");
            var leaderboard = JsonConvert.DeserializeObject<SummitLeaderboard>(eventLeaderboardString);
            string
                eventTitle = DictionaryLookup(thisEventNameId, locale),
                activityResult = $"Score: {activity.Score}",
                vehicleInfo;
            SummitLeaderboardEntries entries = ubiInfo.ProfileId=="0" ? leaderboard.Entries.First() : leaderboard.Entries.First(w => w.ProfileId == ubiInfo.ProfileId);
            if (summitEvent.ConstraintTextId.Contains("60871"))
            {
                vehicleInfo = "Forced Vehicle";
            }
            else
            {
                Model model = Models.FirstOrDefault(w => w.Id == entries.VehicleId);
                Brand brand;
                if (model != null)
                {
                    brand = Brands.FirstOrDefault(w => w.Id == model.BrandId);
                }
                else
                {
                    brand = null;
                }
                vehicleInfo = $"{DictionaryLookup(brand != null ? brand.TextId : "not found", locale)} - {DictionaryLookup(model != null ? model.TextId : "not found", locale)}";
            }
            if (leaderboard.ScoreFormat == "time")
            {
                activityResult = $"Time: {CustomMethod.ScoreToTime(activity.Score)}";
            }
            else if (eventTitle.Contains("SPEEDTRAP"))
            {
                activityResult = $"Speed: {activity.Score.ToString().Insert(3, ".")} km/h";
            }
            else if (eventTitle.Contains("ESCAPE"))
            {
                activityResult = $"Distance: {activity.Score}m";
            }
            TextOptions eventTitleOptions = new(summitCaps15)
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                WrappingLength = eventImage.Width - 10,
                LineSpacing = 0.7f,
                Origin = new PointF(5, 0),
                FallbackFontFamilies = new[] { FontCollection.Get("Noto Sans Mono CJK JP Bold"), FontCollection.Get("Noto Sans Arabic") }
            };
            TextOptions vehicleTextOptions = new(vehicleFont)
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                WrappingLength = eventImage.Width - 10,
                LineSpacing = 0.7f,
                Origin = new PointF(5, eventImage.Height - 62),
                FallbackFontFamilies = new[] { FontCollection.Get("Noto Sans Mono CJK JP Bold"), FontCollection.Get("Noto Sans Arabic") }
            };
            TextOptions baseTopLelft = new(basefont)
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                FallbackFontFamilies = new[] { FontCollection.Get("Noto Sans Mono CJK JP Bold"), FontCollection.Get("Noto Sans Arabic") }
            };
            TextOptions baseTopRight = new(basefont)
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                FallbackFontFamilies = new[] { FontCollection.Get("Noto Sans Mono CJK JP Bold"), FontCollection.Get("Noto Sans Arabic") }
            };

            using Image<Rgba32> titleBar = new(eventImage.Width, 40);
            using Image<Rgba32> scoreBar = new(eventImage.Width, 60);
            scoreBar.Mutate(ctx => ctx.Fill(Color.Black));
            titleBar.Mutate(ctx => ctx.Fill(Color.Black));
            eventImage.Mutate(ctx => ctx
                .DrawImage(scoreBar, new Point(0, eventImage.Height - scoreBar.Height), 0.7f)
                .DrawImage(titleBar, new Point(0, 0), 0.7f)
                .DrawText(eventTitleOptions, eventTitle, Color.White)
                .DrawText(new(baseTopLelft) { Origin = new PointF(5, eventImage.Height - 22) }, $"Rank: {activity.Rank + 1}", Color.White)
                .DrawText(new(baseTopRight) { Origin = new PointF(eventImage.Width - 5, eventImage.Height - 42) }, activityResult, Color.White)
                .DrawText(new(baseTopRight) { Origin = new PointF(eventImage.Width - 5, eventImage.Height - 22) }, $"Points: {activity.Points}", Color.White)
                .DrawText(vehicleTextOptions, vehicleInfo, Color.White)
                );
            Parallel.For(0, summitEvent.Modifiers.Length, (i, state) =>
            {
                Image<Rgba32> modifierImg = new(1, 1);
                try
                {
                    modifierImg = Image.Load<Rgba32>($"Assets/Summit/Modifiers/{summitEvent.Modifiers[i]}.png");
                }
                catch (Exception)
                {
                    modifierImg = Image.Load<Rgba32>($"Assets/Summit/Modifiers/unknown.png");
                }
                Image<Rgba32> ModifierBackground = new(modifierImg.Width, modifierImg.Height);
                ModifierBackground.Mutate(ctx => ctx.Fill(Color.Black));
                var modifierPoint = new Point(i * modifierImg.Width + 20, titleBar.Height + 10);
                eventImage.Mutate(ctx => ctx
                .DrawImage(ModifierBackground, modifierPoint, 0.7f)
                .DrawImage(modifierImg, modifierPoint, 1f));
            });

            return (eventImage, true);
        }
}