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

namespace LiveBot
{
    internal static partial class HubMethods
    {
        private static ulong _summitId;
        public static byte[,][] RewardsImageBitArr { get; set; } = new byte[4, 4][];

        public static Dictionary<string, Dictionary<string, string>> TCHubLocales { get; private set; } = new();

        public static async Task UpdateHubInfo(bool forced = false)
        {
            List<TCHubJson.Summit> JSummit;
            using HttpClient wc = new();
            string JSummitString = string.Empty;
            try
            {
                JSummitString = await wc.GetStringAsync(Program.TheCrewHubJson.Summit);
            }
            catch (WebException e)
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TCHub, e, "Connection error. Either wrong API link, or the Hub is down.");
                return;
            }

            JSummit = JsonConvert.DeserializeObject<List<TCHubJson.Summit>>(JSummitString);
            if (_summitId == JSummit[0].Summit_ID && !forced) return;

            _summitId = JSummit[0].Summit_ID;
            foreach (var item in Program.TheCrewHubJson.Locales)
            {
                TCHubLocales.Add(item.Key, JsonConvert.DeserializeObject<Dictionary<string, string>>(await wc.GetStringAsync(item.Value)));
            }

            Program.JSummit = JSummit;
            Program.TheCrewHub = JsonConvert.DeserializeObject<TCHubJson.TCHub>(await wc.GetStringAsync(Program.TheCrewHubJson.GameData));
            await Parallel.ForEachAsync(JSummit[0].Events, new ParallelOptions(),
                async (Event, Token) =>
                {
                    JSummit[0].Events.First(w => w.ID == Event.ID).Image_Byte = await wc.GetByteArrayAsync($"https://www.thecrew-hub.com/gen/assets/summits/{Event.Img_Path}", Token);
                });
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < JSummit[i].Rewards.Length; j++)
                {
                    if (JSummit[i].Rewards[j].Img_Path == "")
                    {
                        RewardsImageBitArr[i, j] = null;
                    }
                    else
                    {
                        RewardsImageBitArr[i, j] = await wc.GetByteArrayAsync($"https://www.thecrew-hub.com/gen/assets/summits/{JSummit[i].Rewards[j].Img_Path}");
                    }
                }
            }

            Program.Client.Logger.LogInformation(CustomLogEvents.TCHub, "Info downloaded for {SummitId} summit.", JSummit[0].Summit_ID);
        }

    public static async Task DownloadHubNews()
        {
            using HttpClient wc = new();
            wc.DefaultRequestHeaders.Add("Ubi-AppId", "dda77324-f9d6-44ea-9ecb-30e57b286f6d");
            wc.DefaultRequestHeaders.Add("Ubi-localeCode", "us-en");
            string newsString = string.Empty;
            bool connected = true;

            try
            {
                newsString = await wc.GetStringAsync(Program.TheCrewHubJson.News);
            }
            catch (WebException e)
            {
                connected = false;
                Program.Client.Logger.LogInformation(CustomLogEvents.TCHub, e, "Connection error. Either wrong API link, or the Hub is down.");
            }
            if (connected)
            {
                Program.TheCrewHub = JsonConvert.DeserializeObject<TCHubJson.TCHub>(newsString);
            }
        }

        public static string NameIdLookup(string ID, string locale = "en-GB")
        {
            if (locale == null || !TCHubLocales.TryGetValue(locale, out Dictionary<string, string> dictionary))
            {
                dictionary = TCHubLocales.FirstOrDefault(w => w.Key == "en-GB").Value;
            }

            string hubText = dictionary.FirstOrDefault(w => w.Key.Equals(ID)).Value ?? "[Item Name Missing]";
            return WebUtility.HtmlDecode(hubText);
        }

        public static async Task<Image<Rgba32>> BuildEventImage(TCHubJson.Event Event, TCHubJson.Rank Rank, DB.UbiInfo UserInfo, byte[] EventImageBytes, bool isCorner = false, bool isSpecial = false)
        {
            string locale = "en-GB";
            DB.Leaderboard userInfo = DB.DBLists.Leaderboard.FirstOrDefault(w => w.ID_User == UserInfo.Discord_Id);
            if (userInfo != null)
                locale = userInfo.Locale;
            Image<Rgba32> EventImage = Image.Load<Rgba32>(EventImageBytes);
            TCHubJson.Activities Activity = null;
            if (Rank != null)
            {
                Activity = Rank.Activities.FirstOrDefault(w => w.Activity_ID.Equals(Event.ID.ToString()));
            }
            if (Event.Is_Mission && !isSpecial && !isCorner)
            {
                EventImage.Mutate(ctx => ctx
                .Resize(368, 239)
                    );
            }
            else if (isCorner)
            {
                EventImage.Mutate(ctx => ctx
                .Resize(380, 245)
                );
            }
            else if (isSpecial)
            {
                EventImage.Mutate(ctx => ctx
                    .Resize(380, 483)
                    );
            }
            Font basefont = new(Program.Fonts.Get("HurmeGeometricSans4 Black"), 18);
            Font summitCaps15 = new(Program.Fonts.Get("HurmeGeometricSans4 Black"), 15);
            Font vehicleFont = new(Program.Fonts.Get("HurmeGeometricSans4 Black"), 11.5f);
            if (Activity == null)
            {
                using Image<Rgba32> notComplete = new(EventImage.Width, EventImage.Height);
                TextOptions textOptions = new(basefont)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Origin = new PointF(notComplete.Width / 2, notComplete.Height / 2),
                    FallbackFontFamilies = new[] { Program.Fonts.Get("Noto Sans Mono CJK JP Bold"), Program.Fonts.Get("Noto Sans Arabic") }
                };
                notComplete.Mutate(ctx => ctx
                    .Fill(Color.Black)
                    .DrawText(textOptions, "Event not completed!", Color.White)
                    );
                EventImage.Mutate(ctx => ctx
                .DrawImage(notComplete, new Point(0, 0), 0.8f)
                );
                return EventImage;
            }

            using HttpClient wc = new();

            string ThisEventNameID = string.Empty;
            if (Event.Is_Mission)
            {
                ThisEventNameID = Program.TheCrewHub.Missions.Where(w => w.ID == Event.ID).Select(s => s.Text_ID).FirstOrDefault();
            }
            else
            {
                ThisEventNameID = Program.TheCrewHub.Skills.Where(w => w.ID == Event.ID).Select(s => s.Text_ID).FirstOrDefault();
            }
            TCHubJson.SummitLeaderboard leaderboard = JsonConvert.DeserializeObject<TCHubJson.SummitLeaderboard>(await wc.GetStringAsync($"https://api.thecrew-hub.com/v1/summit/{Program.JSummit[0].ID}/leaderboard/{UserInfo.Platform}/{Event.ID}?profile={UserInfo.Profile_Id}"));
            string
                EventTitle = NameIdLookup(ThisEventNameID, locale),
                ActivityResult = $"Score: {Activity.Score}",
                VehicleInfo = string.Empty;
            TCHubJson.SummitLeaderboardEntries Entries = leaderboard.Entries.FirstOrDefault(w => w.Profile_ID == UserInfo.Profile_Id);
            if (Event.Constraint_Text_ID.Contains("60871"))
            {
                VehicleInfo = "Forced Vehicle";
            }
            else
            {
                TCHubJson.Model Model = Program.TheCrewHub.Models.FirstOrDefault(w => w.ID == Entries.Vehicle_ID);
                TCHubJson.Brand Brand;
                if (Model != null)
                {
                    Brand = Program.TheCrewHub.Brands.FirstOrDefault(w => w.ID == Model.Brand_ID);
                }
                else
                {
                    Brand = null;
                }
                VehicleInfo = $"{NameIdLookup(Brand != null ? Brand.Text_ID : "not found", locale)} - {NameIdLookup(Model != null ? Model.Text_ID : "not found", locale)}";
            }
            if (leaderboard.Score_Format == "time")
            {
                ActivityResult = $"Time: {CustomMethod.ScoreToTime(Activity.Score)}";
            }
            else if (EventTitle.Contains("SPEEDTRAP"))
            {
                ActivityResult = $"Speed: {Activity.Score.ToString().Insert(3, ".")} km/h";
            }
            else if (EventTitle.Contains("ESCAPE"))
            {
                ActivityResult = $"Distance: {Activity.Score}m";
            }
            TextOptions EventTitleOptions = new(summitCaps15)
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                WrappingLength = EventImage.Width - 10,
                LineSpacing = 0.7f,
                Origin = new PointF(5, 0),
                FallbackFontFamilies = new[] { Program.Fonts.Get("Noto Sans Mono CJK JP Bold"), Program.Fonts.Get("Noto Sans Arabic") }
            };
            TextOptions VehicleTextOptions = new(vehicleFont)
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                WrappingLength = EventImage.Width - 10,
                LineSpacing = 0.7f,
                Origin = new PointF(5, EventImage.Height - 62),
                FallbackFontFamilies = new[] { Program.Fonts.Get("Noto Sans Mono CJK JP Bold"), Program.Fonts.Get("Noto Sans Arabic") }
            };
            TextOptions BaseTopLelft = new(basefont)
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                FallbackFontFamilies = new[] { Program.Fonts.Get("Noto Sans Mono CJK JP Bold"), Program.Fonts.Get("Noto Sans Arabic") }
            };
            TextOptions BaseTopRight = new(basefont)
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                FallbackFontFamilies = new[] { Program.Fonts.Get("Noto Sans Mono CJK JP Bold"), Program.Fonts.Get("Noto Sans Arabic") }
            };

            using Image<Rgba32> TitleBar = new(EventImage.Width, 40);
            using Image<Rgba32> ScoreBar = new(EventImage.Width, 60);
            ScoreBar.Mutate(ctx => ctx.Fill(Color.Black));
            TitleBar.Mutate(ctx => ctx.Fill(Color.Black));
            EventImage.Mutate(ctx => ctx
                .DrawImage(ScoreBar, new Point(0, EventImage.Height - ScoreBar.Height), 0.7f)
                .DrawImage(TitleBar, new Point(0, 0), 0.7f)
                .DrawText(EventTitleOptions, EventTitle, Color.White)
                .DrawText(new(BaseTopLelft) { Origin = new PointF(5, EventImage.Height - 22) }, $"Rank: {Activity.Rank + 1}", Color.White)
                .DrawText(new(BaseTopRight) { Origin = new PointF(EventImage.Width - 5, EventImage.Height - 42) }, ActivityResult, Color.White)
                .DrawText(new(BaseTopRight) { Origin = new PointF(EventImage.Width - 5, EventImage.Height - 22) }, $"Points: {Activity.Points}", Color.White)
                .DrawText(VehicleTextOptions, VehicleInfo, Color.White)
                );
            Parallel.For(0, Event.Modifiers.Length, (i, state) =>
            {
                Image<Rgba32> ModifierImg = new(1, 1);
                try
                {
                    ModifierImg = Image.Load<Rgba32>($"Assets/Summit/Modifiers/{Event.Modifiers[i]}.png");
                }
                catch (Exception)
                {
                    ModifierImg = Image.Load<Rgba32>($"Assets/Summit/Modifiers/unknown.png");
                }
                Image<Rgba32> ModifierBackground = new(ModifierImg.Width, ModifierImg.Height);
                ModifierBackground.Mutate(ctx => ctx.Fill(Color.Black));
                var modifierPoint = new Point(i * ModifierImg.Width + 20, TitleBar.Height + 10);
                EventImage.Mutate(ctx => ctx
                .DrawImage(ModifierBackground, modifierPoint, 0.7f)
                .DrawImage(ModifierImg, modifierPoint, 1f));
            });

            return EventImage;
        }

        [GeneratedRegex("<(\\w|[=\" #'/]){0,}>")]
        private static partial Regex itemTextRegex();
    }
}