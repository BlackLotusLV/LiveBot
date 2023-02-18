using System.Net;
using System.Net.Http;
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
    Task<ITheCrewHubService> GetSummitDataAsync(bool isForced = false);
    string DictionaryLookup(string id, string locale = "en-GB");
    Task GetGameDataAsync(bool isForced = false);
    FontCollection FontCollection { get; set; }

    Task StartServiceAsync();
    Task<Image<Rgba32>> BuildEventImageAsync(Event @event, Rank rank, DB.UbiInfo ubiInfo, byte[] eventImageBytes, bool isCorner = false, bool isSpecial = false);
}
public class TheCrewHubService : ITheCrewHubService
{
    private readonly HttpClient _httpClient;
    //private readonly ILogger _logger;
    private readonly LiveBotDbContext _dbContext;
    public Summit[] Summit { get; set; }
    public Mission[] Missions { get; set; }
    public Skill[] Skills { get; set; }
    public Model[] Models { get; set; }
    public Brand[] Brands { get; set; }
    public Discipline[] Disciplines { get; set; }
    public Family[] Families { get; set; }
    public byte[,][] RewardsImagesBytes { get; set; } = new byte[4, 4][];
    public Dictionary<string, Dictionary<string, string>> Locales { get; private set; } = new();
    public FontCollection FontCollection { get; set; } = new();
    

    public TheCrewHubService(HttpClient httpClient,LiveBotDbContext dbContext)
    {
        _httpClient = httpClient;
        //_logger = logger;
        _dbContext = dbContext;
    }

    public async Task StartServiceAsync()
    {
        await GetGameDataAsync();
        await GetSummitDataAsync();
        
    }    
    public async Task GetGameDataAsync(bool isForced = false)
    {
        string json;
        try
        {
            json = await _httpClient.GetStringAsync("https://api.thecrew-hub.com/v1/data/game?fields=missions,skills,brands,models,disciplines,families");
        }
        catch (WebException e)
        {
            //_logger.LogError(e,"Error while downloading game data from hub.");
            return;
        }

        JObject jsonObject = JObject.Parse(json);
        Skills = jsonObject["skills"].ToObject<Skill[]>();
        Models = jsonObject["models"].ToObject<Model[]>();
        Missions  = jsonObject["missions"].ToObject<Mission[]>();
        Brands  = jsonObject["brands"].ToObject<Brand[]>();
        Disciplines  = jsonObject["disciplines"].ToObject<Discipline[]>();
        Families  = jsonObject["families"].ToObject<Family[]>();
    }

    public async Task<ITheCrewHubService> GetSummitDataAsync(bool isForced = false)
    {
        string json;
        var oldSummit = Summit;
        try
        {
            json = await _httpClient.GetStringAsync("https://api.thecrew-hub.com/v1/data/summit");
        }
        catch (WebException e)
        {
            //_logger.LogError(e, "Error while getting summit data");
            return this;
        }
        
        Summit = JsonConvert.DeserializeObject<Summit[]>(json);
        
        if (Summit == oldSummit && !isForced) return this;
        
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
        //_logger.LogInformation("The Crew Hub information downloaded");
        return this;
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
    
    public async Task<Image<Rgba32>> BuildEventImageAsync(Event @event, Rank rank, DB.UbiInfo ubiInfo, byte[] eventImageBytes, bool isCorner = false, bool isSpecial = false)
        {
            var locale = "en-GB";
            Leaderboard userInfo = await _dbContext.Leaderboard.FirstOrDefaultAsync(w => w.UserDiscordId == ubiInfo.UserDiscordId);
            if (userInfo != null)
                locale = userInfo.Locale;
            var eventImage = Image.Load<Rgba32>(eventImageBytes);
            Activities activity = null;
            if (rank != null)
            {
                activity = rank.Activities.FirstOrDefault(w => w.ActivityId.Equals(@event.Id.ToString()));
            }
            if (@event.IsMission && !isSpecial && !isCorner)
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
                using Image<Rgba32> notComplete = new(eventImage.Width, eventImage.Height);
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
                return eventImage;
            }

            using HttpClient wc = new();

            string ThisEventNameID;
            if (@event.IsMission)
            {
                ThisEventNameID = Missions.Where(w => w.Id == @event.Id).Select(s => s.TextId).FirstOrDefault();
            }
            else
            {
                ThisEventNameID = Skills.Where(w => w.Id == @event.Id).Select(s => s.TextId).FirstOrDefault();
            }
            SummitLeaderboard leaderboard = JsonConvert.DeserializeObject<SummitLeaderboard>(await wc.GetStringAsync($"https://api.thecrew-hub.com/v1/summit/{Summit[0].Id}/leaderboard/{ubiInfo.Platform}/{@event.Id}?profile={ubiInfo.ProfileId}"));
            string
                eventTitle = DictionaryLookup(ThisEventNameID, locale),
                activityResult = $"Score: {activity.Score}",
                vehicleInfo = string.Empty;
            SummitLeaderboardEntries entries = leaderboard.Entries.FirstOrDefault(w => w.ProfileId == ubiInfo.ProfileId);
            if (@event.ConstraintTextId.Contains("60871"))
            {
                vehicleInfo = "Forced Vehicle";
            }
            else
            {
                Model Model = Models.FirstOrDefault(w => w.Id == entries.VehicleId);
                Brand Brand;
                if (Model != null)
                {
                    Brand = Brands.FirstOrDefault(w => w.Id == Model.BrandId);
                }
                else
                {
                    Brand = null;
                }
                vehicleInfo = $"{DictionaryLookup(Brand != null ? Brand.TextId : "not found", locale)} - {DictionaryLookup(Model != null ? Model.TextId : "not found", locale)}";
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
            Parallel.For(0, @event.Modifiers.Length, (i, state) =>
            {
                Image<Rgba32> modifierImg = new(1, 1);
                try
                {
                    modifierImg = Image.Load<Rgba32>($"Assets/Summit/Modifiers/{@event.Modifiers[i]}.png");
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

            return eventImage;
        }
}