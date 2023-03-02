using System.Diagnostics;
using DSharpPlus.SlashCommands;
using LiveBot.Json;
using Newtonsoft.Json;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiveBot.SlashCommands
{
    [SlashCommandGroup("Hub", "Commands in relation to the TheCrew-Hub leaderboards.")]
    public partial class SlashTheCrewHubCommands : ApplicationCommandModule
    {
        private readonly ITheCrewHubService _theCrewHubService;
        private readonly LiveBotDbContext _dbContext;
        public SlashTheCrewHubCommands(ITheCrewHubService theCrewHubService, LiveBotDbContext dbContext)
        {
            _theCrewHubService = theCrewHubService;
            _dbContext = dbContext;
        }
        
        [SlashCommand("Summit", "Shows the tiers and current cut offs for the ongoing summit.")]
        public async Task Summit(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder { Content = "Gathering data and building image." }));
            string PCJson = string.Empty, XBJson = string.Empty, PSJson = string.Empty, StadiaJson = string.Empty;
            string imageLoc = $"{Path.GetTempPath()}{ctx.User.Id}-summit.png";
            float outlineSize = 0.7f;
            byte[] SummitLogo;
            int[,] TierCutoff = new int[,] { { 4000, 8000, 15000 }, { 11000, 21000, 41000 }, { 2100, 4200, 8500 }, { 100, 200, 400 } };
            var JSummit = _theCrewHubService.Summit;

            int platforms = 4;

            using (HttpClient wc = new())
            {
                PCJson = await wc.GetStringAsync($"https://api.thecrew-hub.com/v1/summit/{JSummit[0].Id}/score/pc/profile/a92d844e-9c57-4b8c-a249-108ef42d4500");
                XBJson = await wc.GetStringAsync($"https://api.thecrew-hub.com/v1/summit/{JSummit[0].Id}/score/x1/profile/a92d844e-9c57-4b8c-a249-108ef42d4500");
                PSJson = await wc.GetStringAsync($"https://api.thecrew-hub.com/v1/summit/{JSummit[0].Id}/score/ps4/profile/a92d844e-9c57-4b8c-a249-108ef42d4500");
                try
                {
                    StadiaJson = await wc.GetStringAsync($"https://api.thecrew-hub.com/v1/summit/{JSummit[0].Id}/score/stadia/profile/a92d844e-9c57-4b8c-a249-108ef42d4500");
                }
                catch (Exception)
                {
                    platforms = 3;
                }

                try
                {
                    SummitLogo = await wc.GetByteArrayAsync($"https://www.thecrew-hub.com/gen/assets/summits/{JSummit[0].CoverSmall}");
                }
                catch (WebException e)
                {
                    ctx.Client.Logger.LogError(CustomLogEvents.CommandError, "Summit logo download failed, substituting image.\n{ExceptionMessage}", e.Message);
                    SummitLogo = File.ReadAllBytes("Assets/Summit/summit_small");
                }
            }
            Rank[] events = Array.Empty<Rank>();
            if (platforms == 4)
            {
                events = new Rank[4] { JsonConvert.DeserializeObject<Rank>(PCJson), JsonConvert.DeserializeObject<Rank>(PSJson), JsonConvert.DeserializeObject<Rank>(XBJson), JsonConvert.DeserializeObject<Rank>(StadiaJson) };
            }
            else
            {
                events = new Rank[3] { JsonConvert.DeserializeObject<Rank>(PCJson), JsonConvert.DeserializeObject<Rank>(PSJson), JsonConvert.DeserializeObject<Rank>(XBJson) };
            }
            string[,] pts = new string[platforms, 4];
            for (int i = 0; i < events.Length; i++)
            {
                for (int j = 0; j < events[i].TierEntries.Length; j++)
                {
                    if (events[i].TierEntries[j].Points == 4294967295)
                    {
                        pts[i, j] = "-";
                    }
                    else
                    {
                        pts[i, j] = events[i].TierEntries[j].Points.ToString();
                    }
                }
            }

            using (Image<Rgba32> PCImg = Image.Load<Rgba32>("Assets/Summit/PC.jpeg"))
            using (Image<Rgba32> PSImg = Image.Load<Rgba32>("Assets/Summit/PS.jpg"))
            using (Image<Rgba32> XBImg = Image.Load<Rgba32>("Assets/Summit/XB.png"))
            using (Image<Rgba32> StadiaImg = Image.Load<Rgba32>("Assets/Summit/STADIA.png"))
            using (Image<Rgba32> BaseImg = new(300 * platforms, 643))
            {
                Image<Rgba32>[] PlatformImg = new Image<Rgba32>[4] { PCImg, PSImg, XBImg, StadiaImg };
                Parallel.For(0, events.Length, (i, state) =>
                {
                    using Image<Rgba32> TierImg = Image.Load<Rgba32>("Assets/Summit/SummitBase.png");
                    using Image<Rgba32> SummitImg = Image.Load<Rgba32>(SummitLogo);
                    using Image<Rgba32> FooterImg = new(300, 30);

                    SummitImg.Mutate(ctx => ctx.Crop(300, SummitImg.Height));
                    Color TextColour = Color.WhiteSmoke;
                    Color OutlineColour = Color.DarkSlateGray;

                    Point SummitLocation = new(0 + (300 * i), 0);

                    Parallel.For(0, 4, (j, state) =>
                    {
                        TierImg.Mutate(ctx => ctx
                            .DrawText(
                                new TextOptions(new Font(_theCrewHubService.FontCollection.Get("HurmeGeometricSans4 Black"), 17))
                                {
                                    Origin = new PointF(295, 340 + (j * 70)),
                                    HorizontalAlignment = HorizontalAlignment.Right,
                                    VerticalAlignment = VerticalAlignment.Top
                                },
                                 j == 3 ? "All Participants" : $"Top {TierCutoff[i, j]}",
                                Brushes.Solid(TextColour),
                                Pens.Solid(OutlineColour, outlineSize))
                            );
                    });

                    TierImg.Mutate(ctx => ctx
                    .DrawLines(Color.Black, 1.5f, new PointF(0, 0), new PointF(TierImg.Width, 0), new PointF(TierImg.Width, TierImg.Height), new PointF(0, TierImg.Height))
                    );
                    FooterImg.Mutate(ctx => ctx
                    .Fill(Color.Black)
                    .DrawText(new TextOptions(new Font(_theCrewHubService.FontCollection.Get("HurmeGeometricSans4 Black"), 15)) { Origin = new PointF(10, 10) }, $"TOTAL PARTICIPANTS: {events[i].PlayerCount}", TextColour)
                    );
                    BaseImg.Mutate(ctx => ctx
                        .DrawImage(SummitImg, SummitLocation, 1)
                        .DrawImage(TierImg, SummitLocation, 1)
                        .DrawImage(FooterImg, new Point(0 + (300 * i), 613), 1)
                        .DrawImage(PlatformImg[i], new Point(0 + (300 * i), 0), 1)
                        );
                    Parallel.For(0, 4, (j, state) =>
                    {
                        BaseImg.Mutate(ctx => ctx
                        .DrawText(
                            new TextOptions(new Font(_theCrewHubService.FontCollection.Get("HurmeGeometricSans4 Black"), 30))
                            {
                                Origin = new PointF(80 + (300 * i), 575 - (j * 70))
                            },
                            pts[i, j],
                            Brushes.Solid(TextColour),
                            Pens.Solid(OutlineColour, outlineSize)
                            ));
                    });
                });
                BaseImg.Save(imageLoc);
            }
            using var upFile = new FileStream(imageLoc, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            DiscordFollowupMessageBuilder msgBuilder = new()
            {
                Content = $"Summit tier lists.\n *Summit ends on <t:{JSummit[0].EndDate}>(<t:{JSummit[0].EndDate}:R>)*"
            };
            msgBuilder.AddFile(upFile);
            msgBuilder.AddMention(new UserMention());
            await ctx.FollowUpAsync(msgBuilder);
        }

        private sealed class PlatformOptions : IAutocompleteProvider
        {
            public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                var databaseContext = ctx.Services.GetService<LiveBotDbContext>();
                List<DiscordAutoCompleteChoice> result = new();
                foreach (var item in databaseContext.UbiInfo.Where(w => w.UserDiscordId == ctx.Member.Id))
                {
                    switch (item.Platform)
                    {
                        case "pc":
                            result.Add(new DiscordAutoCompleteChoice("PC", "pc"));
                            break;

                        case "x1":
                            result.Add(new DiscordAutoCompleteChoice("Xbox", "x1"));
                            break;

                        case "ps4":
                            result.Add(new DiscordAutoCompleteChoice("PlayStation", "ps4"));
                            break;

                        case "stadia":
                            result.Add(new DiscordAutoCompleteChoice("Stadia", "stadia"));
                            break;
                    }
                }
                return Task.FromResult((IEnumerable<DiscordAutoCompleteChoice>)result);
            }
        }

        [SlashCommand("my-summit", "Shows your summit scores.")]
        public async Task MySummit(InteractionContext ctx, [Autocomplete(typeof(PlatformOptions))][Option("platform", "Which platform leaderboard you want to see")] string platform = "pc")
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder { Content = "Gathering data and building image." }));
            await _theCrewHubService.GetSummitDataAsync();

            string OutMessage = string.Empty;
            string imageLoc = $"{Path.GetTempPath()}{ctx.User.Id}-mysummit.png";

            bool sendImage = false;

            string search = string.Empty;

            var ubiInfoList = await _dbContext.UbiInfo.Where(w => w.UserDiscordId == ctx.User.Id).ToListAsync();
            UbiInfo UbiInfo;
            if (ubiInfoList.Count == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder() { Content = "Could not find any profile data, please link your ubisoft account with Live bot." });
                return;
            }
            UbiInfo = ubiInfoList[0];
            if (ubiInfoList.Count > 1)
            {
                search = platform switch
                {
                    "x1" => platform,
                    "ps4" => platform,
                    "stadia" => platform,
                    _ => "pc",
                };
                if (ubiInfoList.Count(w => w.Platform.Equals(search)) == 1)
                {
                    UbiInfo = ubiInfoList.FirstOrDefault(w => w.Platform == search);
                }
            }
            string SJson;
            Summit[] jSummit = _theCrewHubService.Summit;
            using (HttpClient wc = new())
            {
                SJson = await wc.GetStringAsync($"https://api.thecrew-hub.com/v1/summit/{jSummit[0].Id}/score/{UbiInfo.Platform}/profile/{UbiInfo.ProfileId}");
            }
            Rank Events = JsonConvert.DeserializeObject<Rank>(SJson);

            if (Events.Points != 0)
            {
                int[,] WidthHeight = new int[,] { { 0, 0 }, { 249, 0 }, { 498, 0 }, { 0, 249 }, { 373, 249 }, { 0, 493 }, { 373, 493 }, { 747, 0 }, { 747, 249 } };
                var AllignTopLeft12 = new TextOptions(new Font(_theCrewHubService.FontCollection.Get("HurmeGeometricSans4 Black"), 12.5f))
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    FallbackFontFamilies = new[] { _theCrewHubService.FontCollection.Get("Noto Sans Mono CJK JP Bold"), _theCrewHubService.FontCollection.Get("Noto Sans Arabic") }
                };
                var AllignTopLeft15 = new TextOptions(new Font(_theCrewHubService.FontCollection.Get("HurmeGeometricSans4 Black"), 15))
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    FallbackFontFamilies = new[] { _theCrewHubService.FontCollection.Get("Noto Sans Mono CJK JP Bold"), _theCrewHubService.FontCollection.Get("Noto Sans Arabic") }
                };

                Image<Rgba32> BaseImage = new(1127, 765);

                await Parallel.ForEachAsync(jSummit[0].Events, new ParallelOptions(), async (Event, token) =>
                {
                    int i = jSummit[0].Events.Select((element, index) => new { element, index })
                           .FirstOrDefault(x => x.element.Equals(Event))?.index ?? -1;
                    Image image = await _theCrewHubService.BuildEventImageAsync(
                        Event,
                        Events,
                        UbiInfo,
                        Event.ImageByte,
                        i == 7,
                        i == 8);
                    BaseImage.Mutate(imageProcessingContext => imageProcessingContext
                    .DrawImage(
                        image,
                        new Point(WidthHeight[i, 0], WidthHeight[i, 1]),
                    1)
                );
                });
                using (Image<Rgba32> TierBar = Image.Load<Rgba32>("Assets/Summit/TierBar.png"))
                {
                    TierBar.Mutate(ctx => ctx.DrawImage(new Image<Rgba32>(new Configuration(), TierBar.Width, TierBar.Height, backgroundColor: Color.Black), new Point(0, 0), 0.35f));
                    int[] TierXPos = new int[4] { 845, 563, 281, 0 };
                    bool[] Tier = new bool[] { false, false, false, false };
                    Parallel.For(0, Events.TierEntries.Length, (i, state) =>
                    {
                        if (Events.TierEntries[i].Points == 4294967295)
                        {
                            Tier[i] = true;
                        }
                        else
                        {
                            if (Events.TierEntries[i].Points <= Events.Points)
                            {
                                Tier[i] = true;
                            }

                            TierBar.Mutate(ctx => ctx
                            .DrawText(new TextOptions(AllignTopLeft12) { Origin = new PointF(TierXPos[i] + 5, 15) }, $"Points Needed: {Events.TierEntries[i].Points}", Color.White)
                            );
                        }
                    });

                    TierBar.Mutate(ctx => ctx
                    .DrawText(new TextOptions(AllignTopLeft15) { Origin = new PointF(TierXPos[Tier.Count(c => c) - 1] + 5, 0) }, $"Summit Rank: {Events.UserRank + 1} Score: {Events.Points}", Color.White)
                    );

                    BaseImage.Mutate(ctx => ctx
                    .DrawImage(TierBar, new Point(0, BaseImage.Height - 30), 1)
                    );
                }
                BaseImage.Save(imageLoc);

                OutMessage = $"{ctx.User.Mention}, Here are your summit event stats for {(UbiInfo.Platform == "x1" ? "Xbox" : UbiInfo.Platform == "ps4" ? "PlayStation" : UbiInfo.Platform == "stadia" ? "Stadia" : "PC")}.\n*Summit ends on <t:{jSummit[0].EndDate}>(<t:{jSummit[0].EndDate}:R>). Scoreboard powered by The Crew Hub*";
                sendImage = true;
            }
            else
            {
                OutMessage = $"{ctx.User.Mention}, You have not completed any summit event!";
            }

            if (sendImage)
            {
                using var upFile = new FileStream(imageLoc, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);

                var msgBuilder = new DiscordFollowupMessageBuilder
                {
                    Content = OutMessage
                };
                msgBuilder.AddFile(upFile);
                msgBuilder.AddMention(new UserMention());
                await ctx.FollowUpAsync(msgBuilder);
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder { Content = OutMessage });
            }
        }

        [SlashCommand("Top-Summit", "Shows the summit board with all the world record scores.")]
        public async Task TopSummit(InteractionContext ctx, [Option("platform", "Which platform leaderboard you want to see")] Platforms platform = Platforms.pc)
        {
            Stopwatch sw = Stopwatch.StartNew();
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder { Content = "Gathering data and building image." }));
            await _theCrewHubService.GetSummitDataAsync();

            int TotalPoints = 0;

            string OutMessage = string.Empty;
            string imageLoc = $"{Path.GetTempPath()}{ctx.User.Id}-topsummit.png";
            string search = string.Empty;

            bool alleventscompleted = true;

            switch (platform)
            {
                case Platforms.pc:
                    search = "pc";
                    break;

                case Platforms.x1:
                    search = "x1";
                    break;

                case Platforms.ps4:
                    search = "ps4";
                    break;
            }

            Summit[] JSummit = _theCrewHubService.Summit;

            int[,] WidthHeight = new int[,] { { 0, 0 }, { 249, 0 }, { 498, 0 }, { 0, 249 }, { 373, 249 }, { 0, 493 }, { 373, 493 }, { 747, 0 }, { 747, 249 } };

            using Image<Rgba32> BaseImage = new(1127, 735);

            await Parallel.ForEachAsync(JSummit[0].Events, new ParallelOptions(), async (Event, token) =>
            {
                using HttpClient wc = new();
                int i = JSummit[0].Events.Select((element, index) => new { element, index })
                       .FirstOrDefault(x => x.element.Equals(Event))?.index ?? -1;
                SummitLeaderboard Activity = JsonConvert.DeserializeObject<SummitLeaderboard>(await wc.GetStringAsync($"https://api.thecrew-hub.com/v1/summit/{JSummit[0].Id}/leaderboard/{search}/{Event.Id}?page_size=1", CancellationToken.None));
                Rank Rank = null;
                DB.UbiInfo ubiInfo = null;
                if (Activity.Entries.Length != 0)
                {
                    Rank = JsonConvert.DeserializeObject<Rank>(await wc.GetStringAsync($"https://api.thecrew-hub.com/v1/summit/{JSummit[0].Id}/score/{search}/profile/{Activity.Entries[0].ProfileId}", CancellationToken.None));
                    ubiInfo = new DB.UbiInfo(_dbContext,ctx.User.Id) { Platform = search, ProfileId = Activity.Entries[0].ProfileId };
                }
                Image image = await _theCrewHubService.BuildEventImageAsync(
                        Event,
                        Rank,
                        ubiInfo,
                        Event.ImageByte,
                        i == 7,
                        i == 8);
                BaseImage.Mutate(ctx => ctx
                .DrawImage(
                    image,
                    new Point(WidthHeight[i, 0], WidthHeight[i, 1]),
                    1)
                );
                TotalPoints += (Activity.Entries.Length != 0 ? Activity.Entries[0].Points : 0);
            });

            if (alleventscompleted)
            {
                TotalPoints += 100000;
            }
            BaseImage.Save(imageLoc);
            OutMessage = $"{ctx.User.Mention}, Here are the top summit scores for {(search == "x1" ? "Xbox" : search == "ps4" ? "PlayStation" : search == "stadia" ? "Stadia" : "PC")}. Total event points: **{TotalPoints}**\n*Summit ends on <t:{JSummit[0].EndDate}>(<t:{JSummit[0].EndDate}:R>). Scoreboard powered by The Crew Hub*";

            using var upFile = new FileStream(imageLoc, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            var msgBuilder = new DiscordFollowupMessageBuilder
            {
                Content = OutMessage
            };
            msgBuilder.AddFile(upFile);
            msgBuilder.AddMention(new UserMention());
            await ctx.FollowUpAsync(msgBuilder);
            Console.WriteLine(sw.Elapsed);
        }

        private sealed class RewardsOptions : IAutocompleteProvider
        {
            public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                var databaseContext = ctx.Services.GetService<LiveBotDbContext>();
                var hub = ctx.Services.GetService<ITheCrewHubService>();
                string locale = "en-GB";
                DB.User userInfo = databaseContext.Users.FirstOrDefault(w => w.DiscordId == ctx.User.Id);
                if (userInfo != null)
                    locale = userInfo.Locale;
                List<DiscordAutoCompleteChoice> result = new();
                for (int i = 0; i < 4; i++)
                {
                    if (hub.Summit[i].TextId != "55475")
                        result.Add(new DiscordAutoCompleteChoice(hub.DictionaryLookup(hub.Summit[i].TextId, locale), i));
                }
                return Task.FromResult((IEnumerable<DiscordAutoCompleteChoice>)result);
            }
        }
        [GeneratedRegex("\\w{0,}_")]
        private partial Regex MatchAffixRegex();
        [GeneratedRegex("((<(\\w||[/=\"'#\\ ]){0,}>)||(&#\\d{0,}; )){0,}")]
        private partial Regex MatchRewardRegex();

        [SlashCommand("Rewards", "Summit rewards for selected date")]
        public async Task Rewards(
            InteractionContext ctx,
            [Autocomplete(typeof(RewardsOptions))][Maximum(3)][Minimum(0)][Option("Summit", "Which summit to see the rewards for.")] long weeknumber = 0)
        {
            string locale = "en-GB";
            DB.User userInfo =await _dbContext.Users.FirstOrDefaultAsync(w => w.DiscordId == ctx.User.Id);
            if (userInfo != null)
                locale = userInfo.Locale;
            Week Week = (Week)weeknumber;
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder { Content = "Gathering data and building image." }));
            await _theCrewHubService.GetSummitDataAsync();

            Color[] RewardColours = new Color[] { Rgba32.ParseHex("#0060A9"), Rgba32.ParseHex("#D5A45F"), Rgba32.ParseHex("#C2C2C2"), Rgba32.ParseHex("#B07C4D") };

            string imageLoc = $"{Path.GetTempPath()}{ctx.User.Id}-summitrewards.png";
            int RewardWidth = 412;
            Reward[] Rewards = _theCrewHubService.Summit[(int)Week].Rewards;
            using (Image<Rgba32> RewardsImage = new(4 * RewardWidth, 328))
            {
                Parallel.For(0, Rewards.Length, (i, state) =>
                {
                    string RewardTitle = string.Empty;

                    Image<Rgba32>
                                affix1 = new(1, 1),
                                affix2 = new(1, 1),
                                affixbonus = new(1, 1),
                                DisciplineForPart = new(1, 1);
                    bool isParts = false;
                    switch (Rewards[i].Type)
                    {
                        case "phys_part":
                            string
                                   affix1name = MatchAffixRegex().Replace(Rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("affix1")).Value ?? "unknown", string.Empty),
                                   affix2name = MatchAffixRegex().Replace(Rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("affix2")).Value ?? "unknown", string.Empty),
                                   affixBonusName = MatchAffixRegex().Replace(Rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("bonus_icon")).Value ?? "unknown", string.Empty);
                            try
                            {
                                affix1 = Image.Load<Rgba32>($"Assets/Affix/{affix1name.ToLower()}.png");
                            }
                            catch
                            {
                                affix1 = Image.Load<Rgba32>($"Assets/Affix/unknown.png");
                            }
                            try
                            {
                                affix2 = Image.Load<Rgba32>($"Assets/Affix/{affix2name.ToLower()}.png");
                            }
                            catch
                            {
                                affix2 = Image.Load<Rgba32>($"Assets/Affix/unknown.png");
                            }
                            try
                            {
                                affixbonus = Image.Load<Rgba32>($"Assets/Affix/{affixBonusName.ToLower()}.png");
                            }
                            catch
                            {
                                affixbonus = Image.Load<Rgba32>($"Assets/Affix/unknown.png");
                            }
                            try
                            {
                                DisciplineForPart = Image.Load<Rgba32>($"Assets/Disciplines/{Rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("vcat_icon")).Value}.png");
                                DisciplineForPart.Mutate(ctx => ctx.Resize((int)(affix1.Width * 1.2f), (int)(affix1.Height * 1.2f), false));
                            }
                            catch
                            {
                                DisciplineForPart = Image.Load<Rgba32>($"Assets/Affix/unknown.png");
                            }
                            StringBuilder sb = new();
                            if (Rewards[i].SubtitleTextId!="")
                            {
                                sb.Append($"{_theCrewHubService.DictionaryLookup(Rewards[i].SubtitleTextId, locale)} ");
                            }
                            sb.Append(_theCrewHubService.DictionaryLookup(Rewards[i].TitleTextId, locale));
                            RewardTitle =sb.ToString();

                            isParts = true;
                            break;

                        case "vanity":
                            RewardTitle = _theCrewHubService.DictionaryLookup(Rewards[i].TitleTextId, locale);
                            if (RewardTitle is null)
                            {
                                if (Rewards[i].ImgPath.Contains("emote"))
                                {
                                    RewardTitle = "Emote";
                                }
                                else
                                {
                                    RewardTitle = "[unknown]";
                                }
                            }
                            break;

                        case "generic":
                            RewardTitle = Rewards[i].DebugTitle;
                            break;

                        case "currency":
                            StringBuilder currencySB = new();
                            if (Rewards[i].Extra.FirstOrDefault(w=>w.Key.Equals("currency_type")).Value.Equals("parts"))
                            {
                                currencySB.Append(_theCrewHubService.DictionaryLookup("55508", locale));
                            }
                            else
                            {
                                currencySB.Append(_theCrewHubService.DictionaryLookup(Rewards[i].TitleTextId, locale));
                            }
                            currencySB.Append($"- {Rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("currency_amount")).Value}");
                            RewardTitle = currencySB.ToString();
                            break;

                        case "vehicle":
                            RewardTitle = $"{_theCrewHubService.DictionaryLookup(Rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("brand_text_id")).Value, locale)} - {_theCrewHubService.DictionaryLookup(Rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("model_text_id")).Value, locale)}";
                            break;

                        default:
                            RewardTitle = "LiveBot needs to be updated to view this reward!";
                            break;
                    }
                    RewardTitle ??= "LiveBot needs to be updated to view this reward!";

                    RewardTitle = MatchRewardRegex().Replace(RewardTitle, string.Empty).ToUpper();

                    using Image<Rgba32> RewardImage = Image.Load<Rgba32>(_theCrewHubService.RewardsImagesBytes[(int)Week,i]);
                    using Image<Rgba32> TopBar = new(RewardImage.Width, 20);
                    TopBar.Mutate(ctx => ctx.
                    Fill(RewardColours[i])
                    );
                    TextOptions TextOptions = new(new Font(_theCrewHubService.FontCollection.Get("HurmeGeometricSans4 Black"), 25))
                    {
                        WrappingLength = RewardWidth,
                        Origin = new PointF(((4 - Rewards[i].Level) * RewardWidth) + 5, 15),
                        FallbackFontFamilies = new[] { _theCrewHubService.FontCollection.Get("Noto Sans Mono CJK JP Bold"), _theCrewHubService.FontCollection.Get("Noto Sans Arabic") }
                    };
                    RewardsImage.Mutate(ctx => ctx
                    .DrawImage(RewardImage, new Point((4 - Rewards[i].Level) * RewardWidth, 0), 1)
                    .DrawImage(TopBar, new Point((4 - Rewards[i].Level) * RewardWidth, 0), 1)
                    .DrawText(TextOptions, RewardTitle, Brushes.Solid(Color.White), Pens.Solid(Color.Black, 1f))
                    );
                    if (isParts)
                    {
                        RewardsImage.Mutate(ctx => ctx
                        .DrawImage(affix1, new Point((4 - Rewards[i].Level) * RewardWidth, RewardImage.Height - affix1.Height), 1)
                        .DrawImage(affix2, new Point((4 - Rewards[i].Level) * RewardWidth + affix1.Width, RewardImage.Height - affix2.Height), 1)
                        .DrawImage(affixbonus, new Point((4 - Rewards[i].Level) * RewardWidth + affix1.Width + affix2.Width, RewardImage.Height - affixbonus.Height), 1)
                        .DrawImage(DisciplineForPart, new Point((4 - Rewards[i].Level) * RewardWidth, RewardsImage.Height - (affix1.Height + DisciplineForPart.Height + 5)), 1)
                        );
                    }
                });
                RewardsImage.Save(imageLoc);
            }
            using var upFile = new FileStream(imageLoc, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            var msgBuilder = new DiscordFollowupMessageBuilder
            {
                Content = $"{ctx.User.Mention}, here are {Week.GetName(Week)} summit rewards:"
            };
            msgBuilder.AddFile(upFile);
            msgBuilder.AddMention(new UserMention());
            await ctx.FollowUpAsync(msgBuilder);
        }
        [GeneratedRegex("https://ubisoft-avatars.akamaized.net/|/default(.*)")]
        private static partial Regex HubLinkRegex();

        [SlashCommand("link-hub", "Links your hub information with Live bot.")]
        public async Task LinkHub(InteractionContext ctx, [Option("link", "Your Ubisoft avatar link.")] string link, [Option("platform", "The platform you want to link")] Platforms platform)
        {
            await ctx.DeferAsync(true);
            link = HubLinkRegex().Replace(link, string.Empty);

            if (!Guid.TryParse(link, out _))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder() { Content = "The link you provided does not contain your Ubisoft Guid. Please check the tutorial again of how to get the right link" });
                return;
            }
            string search = platform switch
            {
                Platforms.pc => "pc",
                Platforms.ps4 => "ps4",
                Platforms.x1 => "x1",
                _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
            };
            DB.UbiInfo info =await _dbContext.UbiInfo.FirstOrDefaultAsync(w => w.Platform == search && w.ProfileId == link);
            if (info != null)
            {
                if (info.UserDiscordId != ctx.User.Id)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder() { Content = "The Ubisoft account you are trying to link has already been linked with a different Discord account." });
                    return;
                }
                await ctx.EditResponseAsync(new DiscordWebhookBuilder() { Content = "These accounts have already been linked" });
                return;
            }

            DB.UbiInfo newEntry = new(_dbContext,ctx.User.Id)
            {
                ProfileId = link,
                Platform = search
            };

            await _dbContext.UbiInfo.AddAsync(newEntry);
            await _dbContext.SaveChangesAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder() { Content = $"Your Discord account has been linked with {link} account ID on {search} platform." });
        }

        [SlashCommand("unlink-hub", "Unlinks a specific hub account from your discord")]
        public async Task UnlinkHub(InteractionContext ctx, [Autocomplete(typeof(LinkedAccountOptions))][Option("Account", "The account to unlink")] long ID)
        {
            await ctx.DeferAsync(true);
            DB.UbiInfo entry = await _dbContext.UbiInfo.FirstOrDefaultAsync(w => w.Id == ID);
            if (entry == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Could not find a listing with this ID, please make sure you select from provided items in the list"));
                return;
            }

            _dbContext.UbiInfo.Remove(entry);
            await _dbContext.SaveChangesAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"You have unlinked **{entry.ProfileId}** - **{entry.Platform}** from your Discord account"));
        }

        private sealed class LinkedAccountOptions : IAutocompleteProvider
        {
            public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                var databaseContext = ctx.Services.GetService<LiveBotDbContext>();
                List<DiscordAutoCompleteChoice> result = new();
                foreach (var item in databaseContext.UbiInfo.Where(w => w.UserDiscordId == ctx.Member.Id))
                {
                    result.Add(new DiscordAutoCompleteChoice($"{item.Platform} - {item.ProfileId}", (long)item.Id));
                }
                return Task.FromResult((IEnumerable<DiscordAutoCompleteChoice>)result);
            }
        }

        [SlashCommand("set-locale","Sets what dictionary to use from the hub")]
        public async Task SetLocale(InteractionContext ctx, [Autocomplete(typeof(LocaleOptions))][Option("Locale","Localisation")] string locale)
        {
            await ctx.DeferAsync(true);
            DB.User userInfo = await _dbContext.Users.FirstOrDefaultAsync(w => w.DiscordId == ctx.User.Id);
            if (userInfo==null)
            {
                await _dbContext.Users.AddAsync(new User(_dbContext, ctx.User.Id) { Locale = locale });
                await _dbContext.SaveChangesAsync();
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Your locale has been set"));
                return;
            }

            userInfo.Locale = locale;
            _dbContext.Users.Update(userInfo);
            await _dbContext.SaveChangesAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Your locale has been set"));
        }
        private sealed class LocaleOptions : IAutocompleteProvider
        {
            public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                var hub = ctx.Services.GetService<ITheCrewHubService>();
                List<DiscordAutoCompleteChoice> result = new();
                foreach (string item in hub.Locales.Keys)
                {
                    string locale = item switch
                    {
                        "en-GB" => "English",
                        "ja" => "日本語",
                        "ko" => "한국어",
                        "es-ES" => "Español",
                        "ru" => "Русский",
                        "pt-BR" => "Português",
                        "fr" => "Français",
                        "de" => "Deutsch",
                        "it" => "Italiano",
                        "nl" => "Nederlands",
                        "pl" => "Polski",
                        "ar" => "العربية",
                        "zh-CN" => "简体中文",
                        "zh-TW" => "繁體中文",
                        _ => item
                    };
                    result.Add(new DiscordAutoCompleteChoice(locale, item));
                }
                return Task.FromResult((IEnumerable<DiscordAutoCompleteChoice>)result);
            }
        }

        public enum Week
        {
            [ChoiceName("This Week")]
            ThisWeek = 0,

            [ChoiceName("Next Week")]
            NextWeek = 1,

            [ChoiceName("3rd Week")]
            ThirdWeek = 2,

            [ChoiceName("4th Week")]
            ForthWeek = 3,
        }

        public enum Platforms
        {
            [ChoiceName("PC")]
            pc,

            [ChoiceName("PlayStation")]
            ps4,

            [ChoiceName("Xbox")]
            x1
        }
    }
}