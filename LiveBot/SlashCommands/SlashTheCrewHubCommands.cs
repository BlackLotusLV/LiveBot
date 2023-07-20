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
        public ITheCrewHubService TheCrewHubService { private get; set; }
        public LiveBotDbContext DatabaseService { private get; set; }
        public IDatabaseMethodService DatabaseMethodService { private get; set; }
        
        [SlashCommand("Summit", "Shows the tiers and current cut offs for the ongoing summit.")]
        public async Task Summit(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder { Content = "Gathering data and building image." }));
            var imageLoc = $"{Path.GetTempPath()}{ctx.User.Id}-summit.png";
            const float outlineSize = 0.7f;
            byte[] summitLogo;
            var tierCutoffDictionary = new Dictionary<string, int[]>
            {
                { "pc", new[] { 4000, 8000, 15000 } },
                { "ps4", new[] { 11000, 21000, 41000 } },
                { "x1", new[]{ 2100, 4200, 8500 } }
            };
            var jSummit = TheCrewHubService.Summit;
            var platforms = new[] { "pc", "x1", "ps4" };
            
            using (HttpClient wc = new())
            {
                try
                {
                    summitLogo = await wc.GetByteArrayAsync($"https://www.thecrew-hub.com/gen/assets/summits/{jSummit[0].CoverSmall}");
                }
                catch (WebException e)
                {
                    ctx.Client.Logger.LogError(CustomLogEvents.CommandError, "Summit logo download failed, substituting image.\n{ExceptionMessage}", e.Message);
                    summitLogo = await File.ReadAllBytesAsync("Assets/Summit/summit_small");
                }
            }
            Image<Rgba32> canvas = new(300 * platforms.Length, 643);
            var summitImg = Image.Load<Rgba32>(summitLogo);
            summitImg.Mutate(imageProcessingContext => imageProcessingContext.Crop(300, summitImg.Height));
            await Parallel.ForEachAsync(platforms, async (platform, token) =>
            {
                int multiplier = platform switch
                {
                    "ps4" => 1,
                    "x1" => 2,
                    _ => 0
                };
                using HttpClient wc = new();
                string summitInfo = await wc.GetStringAsync(
                    $"https://api.thecrew-hub.com/v1/summit/{TheCrewHubService.Summit.First().Id}/score/{platform}/profile/a92d844e-9c57-4b8c-a249-108ef42d4500",token);

                var rank = JsonConvert.DeserializeObject<Rank>(summitInfo);

                var platformImage = Image.Load<Rgba32>($"Assets/Summit/{platform}.jpg");
                var basePlate = Image.Load<Rgba32>("Assets/Summit/SummitBase.png");
                Image<Rgba32> footerImg = new(300, 30);
                Color textColour = Color.WhiteSmoke;
                Color outlineColour = Color.DarkSlateGray;

                Parallel.For(0, 4, (i) =>
                {
                    basePlate.Mutate(ipc=>ipc
                        .DrawText(
                            new TextOptions(new Font(TheCrewHubService.FontCollection.Get("HurmeGeometricSans4 Black"), 17))
                            {
                                Origin = new PointF(295, 340 + (i * 70)),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Top
                            },
                            i == 3 ? "All Participants" : $"Top {tierCutoffDictionary.First(x=>x.Key==platform).Value[i]}",
                            Brushes.Solid(textColour),
                            Pens.Solid(outlineColour, outlineSize))
                    );
                });
                basePlate.Mutate(ipc=>ipc
                    .DrawLines(Color.Black, 1.5f, new PointF(0, 0), new PointF(basePlate.Width, 0), new PointF(basePlate.Width, basePlate.Height), new PointF(0, basePlate.Height)));
                footerImg.Mutate(ipc => ipc
                    .Fill(Color.Black)
                    .DrawText(new TextOptions(new Font(TheCrewHubService.FontCollection.Get("HurmeGeometricSans4 Black"), 15)) { Origin = new PointF(10, 10) }, $"TOTAL PARTICIPANTS: {rank.PlayerCount}", textColour)
                );
                Parallel.For(0, rank.TierEntries.Length, (i) =>
                {
                    basePlate.Mutate(ipc=>ipc
                        .DrawText(
                            new TextOptions(new Font(TheCrewHubService.FontCollection.Get("HurmeGeometricSans4 Black"), 30))
                            {
                                Origin = new PointF(80, 575 - (i * 70))
                            },
                            rank.TierEntries[i].Points ==4294967295? "-":rank.TierEntries[i].Points.ToString(),
                            Brushes.Solid(textColour),
                            Pens.Solid(outlineColour, outlineSize)
                        ));
                });
                
                canvas.Mutate(ipc=>ipc
                    .DrawImage(summitImg,new Point(0+300*multiplier,0),1)
                    .DrawImage(basePlate,new Point(0+300*multiplier,0),1)
                    .DrawImage(footerImg, new Point(0 + (300 * multiplier), 613), 1)
                    .DrawImage(platformImage, new Point(0 + (300 * multiplier), 0), 1)
                );

            });
            await canvas.SaveAsync(imageLoc);


            await using var upFile = new FileStream(imageLoc, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            DiscordFollowupMessageBuilder msgBuilder = new()
            {
                Content = $"Summit tier lists.\n *Summit ends on <t:{jSummit[0].EndDate}>(<t:{jSummit[0].EndDate}:R>)*"
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
            await TheCrewHubService.GetSummitDataAsync();

            string outMessage;
            var imageLoc = $"{Path.GetTempPath()}{ctx.User.Id}-my-summit.png";

            var sendImage = false;

            string search;

            var ubiInfoList = await DatabaseService.UbiInfo.Where(w => w.UserDiscordId == ctx.User.Id).ToListAsync();
            if (ubiInfoList.Count == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder() { Content = "Could not find any profile data, please link your ubisoft account with Live bot." });
                return;
            }
            UbiInfo ubiInfo = ubiInfoList[0];
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
                    ubiInfo = ubiInfoList.FirstOrDefault(w => w.Platform == search);
                }
            }
            string sJson;
            var jSummit = TheCrewHubService.Summit;
            using (HttpClient wc = new())
            {
                sJson = await wc.GetStringAsync($"https://api.thecrew-hub.com/v1/summit/{jSummit[0].Id}/score/{ubiInfo.Platform}/profile/{ubiInfo.ProfileId}");
            }
            var events = JsonConvert.DeserializeObject<Rank>(sJson);

            if (events.Points != 0)
            {
                var widthHeight = new[,] { { 0, 0 }, { 249, 0 }, { 498, 0 }, { 0, 249 }, { 373, 249 }, { 0, 493 }, { 373, 493 }, { 747, 0 }, { 747, 249 } };
                var alignTopLeft12 = new TextOptions(new Font(TheCrewHubService.FontCollection.Get("HurmeGeometricSans4 Black"), 12.5f))
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    FallbackFontFamilies = new[] { TheCrewHubService.FontCollection.Get("Noto Sans Mono CJK JP Bold"), TheCrewHubService.FontCollection.Get("Noto Sans Arabic") }
                };
                var alignTopLeft15 = new TextOptions(new Font(TheCrewHubService.FontCollection.Get("HurmeGeometricSans4 Black"), 15))
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    FallbackFontFamilies = new[] { TheCrewHubService.FontCollection.Get("Noto Sans Mono CJK JP Bold"), TheCrewHubService.FontCollection.Get("Noto Sans Arabic") }
                };

                Image<Rgba32> baseImage = new(1127, 765);
                User user = await DatabaseService.Users.FindAsync(ctx.User.Id);

                await Parallel.ForEachAsync(jSummit[0].Events, new ParallelOptions(), async (summitEvent, _) =>
                {
                    int i = jSummit[0].Events.Select((element, index) => new { element, index })
                        .FirstOrDefault(x => x.element.Equals(summitEvent))?.index ?? -1;
                    (Image image, bool _) = await TheCrewHubService.BuildEventImageAsync(
                        summitEvent,
                        events,
                        ubiInfo,
                        user,
                        summitEvent.ImageByte,
                        i == 7,
                        i == 8);
                    baseImage.Mutate(imageProcessingContext => imageProcessingContext
                        .DrawImage(
                            image,
                            new Point(widthHeight[i, 0], widthHeight[i, 1]),
                            1)
                    );
                });
                var tierBar = Image.Load<Rgba32>("Assets/Summit/TierBar.png");
                tierBar.Mutate(ipc => ipc.DrawImage(new Image<Rgba32>(new Configuration(), tierBar.Width, tierBar.Height, backgroundColor: Color.Black), new Point(0, 0), 0.35f));
                var tierXPos = new[] { 845, 563, 281, 0 };
                var tier = new[] { false, false, false, false };
                Parallel.For(0, events.TierEntries.Length, (i, _) =>
                {
                    if (events.TierEntries[i].Points == 4294967295)
                    {
                        tier[i] = true;
                    }
                    else
                    {
                        if (events.TierEntries[i].Points <= events.Points)
                        {
                            tier[i] = true;
                        }

                        tierBar.Mutate(imageProcessingContext => imageProcessingContext
                            .DrawText(new TextOptions(alignTopLeft12) { Origin = new PointF(tierXPos[i] + 5, 15) }, $"Points Needed: {events.TierEntries[i].Points}", Color.White)
                        );
                    }
                });

                tierBar.Mutate(imageProcessingContext => imageProcessingContext
                    .DrawText(new TextOptions(alignTopLeft15) { Origin = new PointF(tierXPos[tier.Count(c => c) - 1] + 5, 0) }, $"Summit Rank: {events.UserRank + 1} Score: {events.Points}",
                        Color.White)
                );

                baseImage.Mutate(imageProcessingContext => imageProcessingContext
                    .DrawImage(tierBar, new Point(0, baseImage.Height - 30), 1)
                );
                await baseImage.SaveAsync(imageLoc);

                outMessage =
                    $"{ctx.User.Mention}, Here are your summit event stats for {(ubiInfo.Platform == "x1" ? "Xbox" : ubiInfo.Platform == "ps4" ? "PlayStation" : ubiInfo.Platform == "stadia" ? "Stadia" : "PC")}.\n*Summit ends on <t:{jSummit[0].EndDate}>(<t:{jSummit[0].EndDate}:R>). Scoreboard powered by The Crew Hub*";
                sendImage = true;
            }
            else
            {
                outMessage = $"{ctx.User.Mention}, You have not completed any summit event!";
            }

            if (sendImage)
            {
                await using var upFile = new FileStream(imageLoc, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);

                var msgBuilder = new DiscordFollowupMessageBuilder
                {
                    Content = outMessage
                };
                msgBuilder.AddFile(upFile);
                msgBuilder.AddMention(new UserMention());
                await ctx.FollowUpAsync(msgBuilder);
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder { Content = outMessage });
            }
        }

        [SlashCommand("Top-Summit", "Shows the summit board with all the world record scores.")]
        public async Task TopSummit(InteractionContext ctx, [Option("platform", "Which platform leaderboard you want to see")] Platforms platform = Platforms.Pc)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder { Content = "Gathering data and building image." }));
            await TheCrewHubService.GetSummitDataAsync();

            var totalPoints = 0;

            var imageLoc = $"{Path.GetTempPath()}{ctx.User.Id}-top-summit.png";
            var search = string.Empty;

            var allEventsCompleted = true;

            search = platform switch
            {
                Platforms.Pc => "pc",
                Platforms.X1 => "x1",
                Platforms.Ps4 => "ps4",
                _ => search
            };

            var jSummit = TheCrewHubService.Summit;

            var widthHeight = new[,] { { 0, 0 }, { 249, 0 }, { 498, 0 }, { 0, 249 }, { 373, 249 }, { 0, 493 }, { 373, 493 }, { 747, 0 }, { 747, 249 } };

            using Image<Rgba32> baseImage = new(1127, 735);

            var ubiInfo = new UbiInfo(DatabaseService, 6969);
            User user = await DatabaseService.Users.FindAsync(ctx.User.Id);

            await Parallel.ForEachAsync(jSummit[0].Events, new ParallelOptions(), async (summitEvent, _) =>
            {
                using HttpClient wc = new();
                int i = jSummit[0].Events.Select((element, index) => new { element, index })
                       .FirstOrDefault(x => x.element.Equals(summitEvent))?.index ?? -1;
                var activity = JsonConvert.DeserializeObject<SummitLeaderboard>(await wc.GetStringAsync($"https://api.thecrew-hub.com/v1/summit/{jSummit[0].Id}/leaderboard/{search}/{summitEvent.Id}?page_size=1", CancellationToken.None));
                Rank rank = null;
                if (activity.Entries.Length != 0)
                {
                    rank = JsonConvert.DeserializeObject<Rank>(await wc.GetStringAsync($"https://api.thecrew-hub.com/v1/summit/{jSummit[0].Id}/score/{search}/profile/{activity.Entries[0].ProfileId}", CancellationToken.None));
                    ubiInfo.Platform = search;
                    ubiInfo.ProfileId = "0";
                }
                (var image, bool isCompleted) = await TheCrewHubService.BuildEventImageAsync(
                    summitEvent,
                    rank,
                    ubiInfo,
                    user,
                    summitEvent.ImageByte,
                    i == 7,
                    i == 8);
                if (!isCompleted)
                {
                    allEventsCompleted = false;
                }
                baseImage.Mutate(imageProcessingContext => imageProcessingContext
                .DrawImage(
                    image,
                    new Point(widthHeight[i, 0], widthHeight[i, 1]),
                    1)
                );
                totalPoints += (activity.Entries.Length != 0 ? activity.Entries[0].Points : 0);
            });

            if (allEventsCompleted)
            {
                totalPoints += 100000;
            }
            await baseImage.SaveAsync(imageLoc);
            var outMessage = $"{ctx.User.Mention}, Here are the top summit scores for {(search switch
            {
                "x1" => "Xbox",
                "ps4" => "PlayStation",
                "stadia" => "Stadia",
                _ => "PC"
            })}. Total event points: **{totalPoints}**\n*Summit ends on <t:{jSummit[0].EndDate}>(<t:{jSummit[0].EndDate}:R>). Scoreboard powered by The Crew Hub*";

            await using var upFile = new FileStream(imageLoc, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            var msgBuilder = new DiscordFollowupMessageBuilder
            {
                Content = outMessage
            };
            msgBuilder.AddFile(upFile);
            msgBuilder.AddMention(new UserMention());
            await ctx.FollowUpAsync(msgBuilder);
        }

        private sealed class RewardsOptions : IAutocompleteProvider
        {
            public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                var databaseContext = ctx.Services.GetService<LiveBotDbContext>();
                var hub = ctx.Services.GetService<ITheCrewHubService>();
                var locale = "en-GB";
                User userInfo = databaseContext.Users.FirstOrDefault(w => w.DiscordId == ctx.User.Id);
                if (userInfo != null)
                    locale = userInfo.Locale;
                List<DiscordAutoCompleteChoice> result = new();
                for (var i = 0; i < 4; i++)
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
            [Autocomplete(typeof(RewardsOptions))] [Maximum(3)] [Minimum(0)] [Option("Summit", "Which summit to see the rewards for.")]
            long weekNumber = 0)
        {
            var locale = "en-GB";
            User userInfo = await DatabaseService.Users.FindAsync(ctx.User.Id);
            if (userInfo != null)
                locale = userInfo.Locale;
            var week = (Week)weekNumber;
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder(new DiscordMessageBuilder { Content = "Gathering data and building image." }));
            await TheCrewHubService.GetSummitDataAsync();

            var rewardColours = new Color[] { Rgba32.ParseHex("#0060A9"), Rgba32.ParseHex("#D5A45F"), Rgba32.ParseHex("#C2C2C2"), Rgba32.ParseHex("#B07C4D") };

            var imageLoc = $"{Path.GetTempPath()}{ctx.User.Id}-summit-rewards.png";
            const int rewardWidth = 412;
            var rewards = TheCrewHubService.Summit[(int)week].Rewards;
            Image<Rgba32> rewardsImage = new(4 * rewardWidth, 328);
            Parallel.For(0, rewards.Length, (i, _) =>
            {
                string rewardTitle;

                Image<Rgba32>
                    affix1 = new(1, 1),
                    affix2 = new(1, 1),
                    affixBonus = new(1, 1),
                    disciplineForPart = new(1, 1);
                var isParts = false;
                switch (rewards[i].Type)
                {
                    case "phys_part":
                        string
                            affix1Name = MatchAffixRegex().Replace(rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("affix1")).Value ?? "unknown", string.Empty),
                            affix2Name = MatchAffixRegex().Replace(rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("affix2")).Value ?? "unknown", string.Empty),
                            affixBonusName = MatchAffixRegex().Replace(rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("bonus_icon")).Value ?? "unknown", string.Empty);
                        try
                        {
                            affix1 = Image.Load<Rgba32>($"Assets/Affix/{affix1Name.ToLower()}.png");
                        }
                        catch
                        {
                            affix1 = Image.Load<Rgba32>($"Assets/Affix/unknown.png");
                        }

                        try
                        {
                            affix2 = Image.Load<Rgba32>($"Assets/Affix/{affix2Name.ToLower()}.png");
                        }
                        catch
                        {
                            affix2 = Image.Load<Rgba32>($"Assets/Affix/unknown.png");
                        }

                        try
                        {
                            affixBonus = Image.Load<Rgba32>($"Assets/Affix/{affixBonusName.ToLower()}.png");
                        }
                        catch
                        {
                            affixBonus = Image.Load<Rgba32>($"Assets/Affix/unknown.png");
                        }

                        try
                        {
                            disciplineForPart = Image.Load<Rgba32>($"Assets/Disciplines/{rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("vcat_icon")).Value}.png");
                            disciplineForPart.Mutate(imageProcessingContext => imageProcessingContext.Resize((int)(affix1.Width * 1.2f), (int)(affix1.Height * 1.2f), false));
                        }
                        catch
                        {
                            disciplineForPart = Image.Load<Rgba32>($"Assets/Affix/unknown.png");
                        }

                        StringBuilder sb = new();
                        if (rewards[i].SubtitleTextId != "")
                        {
                            sb.Append($"{TheCrewHubService.DictionaryLookup(rewards[i].SubtitleTextId, locale)} ");
                        }

                        sb.Append(TheCrewHubService.DictionaryLookup(rewards[i].TitleTextId, locale));
                        rewardTitle = sb.ToString();

                        isParts = true;
                        break;

                    case "vanity":
                        rewardTitle = TheCrewHubService.DictionaryLookup(rewards[i].TitleTextId, locale) ?? (rewards[i].ImgPath.Contains("emote") ? "Emote" : "[unknown]");

                        break;

                    case "generic":
                        rewardTitle = rewards[i].DebugTitle;
                        break;

                    case "currency":
                        StringBuilder currencyStringBuilder = new();
                        currencyStringBuilder.Append(rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("currency_type")).Value.Equals("parts")
                            ? TheCrewHubService.DictionaryLookup("55508", locale)
                            : TheCrewHubService.DictionaryLookup(rewards[i].TitleTextId, locale));

                        currencyStringBuilder.Append($"- {rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("currency_amount")).Value}");
                        rewardTitle = currencyStringBuilder.ToString();
                        break;

                    case "vehicle":
                        rewardTitle =
                            $"{TheCrewHubService.DictionaryLookup(rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("brand_text_id")).Value, locale)} - {TheCrewHubService.DictionaryLookup(rewards[i].Extra.FirstOrDefault(w => w.Key.Equals("model_text_id")).Value, locale)}";
                        break;

                    default:
                        rewardTitle = "LiveBot needs to be updated to view this reward!";
                        break;
                }

                rewardTitle ??= "LiveBot needs to be updated to view this reward!";

                rewardTitle = MatchRewardRegex().Replace(rewardTitle, string.Empty).ToUpper();

                Image<Rgba32> rewardImage = Image.Load<Rgba32>(TheCrewHubService.RewardsImagesBytes[(int)week, i]);
                Image<Rgba32> topBar = new(rewardImage.Width, 20);
                topBar.Mutate(imageProcessingContext => imageProcessingContext.Fill(rewardColours[i])
                );
                TextOptions textOptions = new(new Font(TheCrewHubService.FontCollection.Get("HurmeGeometricSans4 Black"), 25))
                {
                    WrappingLength = rewardWidth,
                    Origin = new PointF(((4 - rewards[i].Level) * rewardWidth) + 5, 15),
                    FallbackFontFamilies = new[] { TheCrewHubService.FontCollection.Get("Noto Sans Mono CJK JP Bold"), TheCrewHubService.FontCollection.Get("Noto Sans Arabic") }
                };
                rewardsImage.Mutate(ipc => ipc
                    .DrawImage(rewardImage, new Point((4 - rewards[i].Level) * rewardWidth, 0), 1)
                    .DrawImage(topBar, new Point((4 - rewards[i].Level) * rewardWidth, 0), 1)
                    .DrawText(textOptions, rewardTitle, Brushes.Solid(Color.White), Pens.Solid(Color.Black, 1f))
                );
                if (isParts)
                {
                    rewardsImage.Mutate(imageProcessingContext => imageProcessingContext
                        .DrawImage(affix1, new Point((4 - rewards[i].Level) * rewardWidth, rewardImage.Height - affix1.Height), 1)
                        .DrawImage(affix2, new Point((4 - rewards[i].Level) * rewardWidth + affix1.Width, rewardImage.Height - affix2.Height), 1)
                        .DrawImage(affixBonus, new Point((4 - rewards[i].Level) * rewardWidth + affix1.Width + affix2.Width, rewardImage.Height - affixBonus.Height), 1)
                        .DrawImage(disciplineForPart, new Point((4 - rewards[i].Level) * rewardWidth, rewardsImage.Height - (affix1.Height + disciplineForPart.Height + 5)), 1)
                    );
                }
            });
            await rewardsImage.SaveAsync(imageLoc);
            await using var upFile = new FileStream(imageLoc, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            var msgBuilder = new DiscordFollowupMessageBuilder
            {
                Content = $"{ctx.User.Mention}, here are {Enum.GetName(week)} summit rewards:"
            };
            msgBuilder.AddFile(upFile);
            msgBuilder.AddMention(new UserMention());
            await ctx.FollowUpAsync(msgBuilder);
        }

        [GeneratedRegex("https://(ubisoft-avatars.akamaized.net|avatars.ubisoft.com)/|/default(.*)")]
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
                Platforms.Pc => "pc",
                Platforms.Ps4 => "ps4",
                Platforms.X1 => "x1",
                _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
            };
            UbiInfo info =await DatabaseService.UbiInfo.FirstOrDefaultAsync(w => w.Platform == search && w.ProfileId == link);
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

            UbiInfo newEntry = new(DatabaseService,ctx.User.Id)
            {
                ProfileId = link,
                Platform = search
            };

            await DatabaseService.UbiInfo.AddAsync(newEntry);
            await DatabaseService.SaveChangesAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder() { Content = $"Your Discord account has been linked with {link} account ID on {search} platform." });
        }

        [SlashCommand("unlink-hub", "Unlinks a specific hub account from your discord")]
        public async Task UnlinkHub(InteractionContext ctx, [Autocomplete(typeof(LinkedAccountOptions))][Option("Account", "The account to unlink")] long id)
        {
            await ctx.DeferAsync(true);
            UbiInfo entry = await DatabaseService.UbiInfo.FindAsync((int)id);
            if (entry == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Could not find a listing with this ID, please make sure you select from provided items in the list"));
                return;
            }

            DatabaseService.UbiInfo.Remove(entry);
            await DatabaseService.SaveChangesAsync();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"You have unlinked **{entry.ProfileId}** - **{entry.Platform}** from your Discord account"));
        }

        private sealed class LinkedAccountOptions : IAutocompleteProvider
        {
            public Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
            {
                var databaseContext = ctx.Services.GetService<LiveBotDbContext>();
                List<DiscordAutoCompleteChoice> result = new();
                foreach (UbiInfo item in databaseContext.UbiInfo.Where(w => w.UserDiscordId == ctx.Member.Id))
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
            User userInfo = await DatabaseService.Users.FindAsync(ctx.User.Id) ?? await DatabaseMethodService.AddUserAsync(new User(ctx.User.Id));
            userInfo.Locale = locale;
            DatabaseService.Users.Update(userInfo);
            await DatabaseService.SaveChangesAsync();
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
            Pc,

            [ChoiceName("PlayStation")]
            Ps4,

            [ChoiceName("Xbox")]
            X1
        }
    }
}