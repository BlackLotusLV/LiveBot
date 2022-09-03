using System.Diagnostics;

namespace LiveBot.DB
{
    internal static class DBLists
    {
        public static readonly int TableCount = 16;
        public static int LoadedTableCount { get; set; } = 0;

        public static List<VehicleList> VehicleList { get; set; } = new(); //1
        public static List<DisciplineList> DisciplineList { get; set; } = new();//2
        public static List<StreamNotifications> StreamNotifications { get; set; } = new();//3
        public static List<Leaderboard> Leaderboard { get; set; } = new();//4
        public static List<ServerRanks> ServerRanks { get; set; } = new();//5
        public static List<Warnings> Warnings { get; set; } = new();//6
        public static List<ServerSettings> ServerSettings { get; set; } = new();//7
        public static List<RankRoles> RankRoles { get; set; } = new();//8
        public static List<BotOutputList> BotOutputList { get; set; } = new();//9
        public static List<AMBannedWords> AMBannedWords { get; set; } = new();//10
        public static List<ModMail> ModMail { get; set; } = new();//11
        public static List<RoleTagSettings> RoleTagSettings { get; set; } = new();//12
        public static List<ServerWelcomeSettings> ServerWelcomeSettings { get; set; } = new();//13
        public static List<ButtonRoles> ButtonRoles { get; set; } = new();//14
        public static List<UbiInfo> UbiInfo { get; set; } = new();//15
        public static List<UserActivity> UserActivity { get; set; } = new();//16

        public static void LoadAllLists()
        {
            CustomMethod.DBProgress(LoadedTableCount, TimeSpan.Zero);
            Stopwatch sw = Stopwatch.StartNew();
            LoadServerSettings(true, sw);
            new Thread(() =>
            {
                Parallel.Invoke(
                    () => LoadServerWelcomeSettings(true, sw),
                    () => LoadVehicleList(true, sw),
                    () => LoadDisciplineList(true, sw),
                    () => LoadStreamNotifications(true, sw),
                    () => LoadLeaderboard(true, sw),
                    () => LoadServerRanks(true, sw),
                    () => LoadWarnings(true, sw),
                    () => LoadRankRoles(true, sw),
                    () => LoadBannedWords(true, sw),
                    () => LoadBotOutputList(true, sw),
                    () => LoadModMail(true, sw),
                    () => LoadRoleTagSettings(true, sw),
                    () => LoadButtonRoles(true, sw),
                    () => LoadUbiInfo(true, sw),
                    () => LoadUserActivity(true, sw)
                    );
                sw.Stop();
            }).Start();
        }

        #region Load Functions

        public static void LoadUserActivity(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new UserActivityContext();
            UserActivity = ctx.UserActivity.ToList();
            if (check) timer.Stop();
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "User Activity");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"User Activity List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadUbiInfo(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new UbiInfoContext();
            UbiInfo = ctx.UbiInfo.ToList();
            if (check) timer.Stop();
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "UbiInfo");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"UbiInfo List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadVehicleList(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new VehicleListContext();
            VehicleList = (from c in ctx.VehicleList
                           select c).ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "Vehicle");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"Vehicle List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadDisciplineList(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new DisciplineListContext();
            DisciplineList = (from c in ctx.DisciplineList
                              select c).ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "Discipline");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"Discipline List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadStreamNotifications(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new StreamNotificationsContext();
            StreamNotifications = (from c in ctx.StreamNotifications
                                   select c).ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "Stream Notifications");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"Stream Notifications List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadLeaderboard(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new LeaderboardContext();
            Leaderboard = (from c in ctx.Leaderboard
                           select c).ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "Leaderboard");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"Leaderboard List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadServerRanks(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new ServerRanksContext();
            ServerRanks = (from c in ctx.ServerRanks
                           select c).ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "Server Ranks");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"Server Ranks List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadWarnings(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new WarningsContext();
            Warnings = (from c in ctx.Warnings
                        select c).ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "Warnings");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"Warnings List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadServerSettings(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new ServerSettingsContext();
            ServerSettings = (from c in ctx.ServerSettings
                              select c).ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "Server Settings");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"Server Settings List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadServerWelcomeSettings(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new ServerWelcomeSettingsContext();
            ServerWelcomeSettings = (from c in ctx.ServerWelcomeSettings
                                     select c).ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "Server Welcome Settings");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"Server Welcome Settings List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadRankRoles(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new RankRolesContext();
            RankRoles = (from c in ctx.RankRoles
                         select c).ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "Rank Roles");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"Rank Roles List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadBannedWords(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new AMBannedWordsContext();
            AMBannedWords = (from c in ctx.AMBannedWords
                             select c).ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "Banned Words");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"Banned Words List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadBotOutputList(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new BotOutputListContext();
            BotOutputList = (from c in ctx.BotOutputList
                             select c).ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "BotOutputList");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"BotOutputList List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadModMail(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new ModMailContext();
            ModMail = (from c in ctx.ModMail
                       select c).ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "ModMail");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"ModMail List Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadRoleTagSettings(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using var ctx = new RoleTagSettingsContext();
            RoleTagSettings = (from c in ctx.RoleTagSettings
                               select c).ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "RoleTagSettings");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"RoleTag Settings Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        public static void LoadButtonRoles(bool progress = false, Stopwatch timer = null)
        {
            bool check = false;
            if (timer == null)
            {
                timer = Stopwatch.StartNew();
                check = true;
            }
            using ButtonRolesContext ctx = new();
            ButtonRoles = ctx.ButtonRoles.ToList();
            if (check)
            {
                timer.Stop();
            }
            if (progress)
            {
                LoadedTableCount++;
                CustomMethod.DBProgress(LoadedTableCount, timer.Elapsed, "ButtonRoles");
            }
            else
            {
                Program.Client.Logger.LogInformation(CustomLogEvents.TableLoaded, @"ButtonRoles Loaded [{seconds}.{miliseconds}]", timer.Elapsed.Seconds, timer.Elapsed.Milliseconds.ToString("D3"));
            }
        }

        #endregion Load Functions

        #region Update Functions

        public static void UpdateUserActivity(params UserActivity[] o)
        {
            using var ctx = new UserActivityContext();
            ctx.UpdateRange(o);
            ctx.SaveChanges();
        }
        public static void UpdateUbiInfo(params UbiInfo[] o)
        {
            using var ctx = new UbiInfoContext();
            ctx.UpdateRange(o);
            ctx.SaveChanges();
        }

        public static void UpdateLeaderboard(params Leaderboard[] o)
        {
            using var ctx = new LeaderboardContext();
            ctx.UpdateRange(o.Select(w => { w.Cookie_Date = DateTime.SpecifyKind(w.Cookie_Date, DateTimeKind.Utc); return w; }).ToList());
            ctx.SaveChanges();
        }

        public static void UpdateServerRanks(params ServerRanks[] o)
        {
            using var ctx = new ServerRanksContext();
            ctx.UpdateRange(o);
            ctx.SaveChanges();
        }

        public static void UpdateWarnings(params Warnings[] o)
        {
            using var ctx = new WarningsContext();
            ctx.UpdateRange(o);
            ctx.SaveChanges();
        }

        public static void UpdateVehicleList(params VehicleList[] o)
        {
            using var ctx = new VehicleListContext();
            ctx.UpdateRange(o);
            ctx.SaveChanges();
        }

        public static void UpdateServerSettings(params ServerSettings[] o)
        {
            using var ctx = new ServerSettingsContext();
            ctx.UpdateRange(o);
            ctx.SaveChanges();
        }

        public static void UpdateRankRoles(params RankRoles[] o)
        {
            using var ctx = new RankRolesContext();
            ctx.UpdateRange(o);
            ctx.SaveChanges();
        }

        public static void UpdateBannedWords(params AMBannedWords[] o)
        {
            using var ctx = new AMBannedWordsContext();
            ctx.UpdateRange(o);
            ctx.SaveChanges();
        }

        public static void UpdateBotOutputList(params BotOutputList[] o)
        {
            using var ctx = new BotOutputListContext();
            ctx.UpdateRange(o);
            ctx.SaveChanges();
        }

        public static void UpdateModMail(params ModMail[] o)
        {
            using var ctx = new ModMailContext();
            ctx.UpdateRange(o);
            ctx.SaveChanges();
        }

        public static void UpdateRoleTagSettings(params RoleTagSettings[] o)
        {
            using var ctx = new RoleTagSettingsContext();
            ctx.UpdateRange(o);
            ctx.SaveChanges();
        }

        public static void UpdateServerWelcomeSEttings(params ServerWelcomeSettings[] o)
        {
            using ServerWelcomeSettingsContext ctx = new();
            ctx.UpdateRange(o);
            ctx.SaveChanges();
        }

        #endregion Update Functions

        #region Insert Functions

        public static void InsertUserActivity(UserActivity o)
        {
            if (ServerRanks.FirstOrDefault(w => w.User_ID == o.User_ID && w.Server_ID == o.Guild_ID) == null)
            {
                o.Server_Ranks_ID = InsertServerRanks(new ServerRanks { User_ID = o.User_ID, Server_ID = o.Guild_ID });
            }
            else
            {
                o.Server_Ranks_ID = ServerRanks.FirstOrDefault(w => w.User_ID == o.User_ID && w.Server_ID == o.Guild_ID).ID_Server_Rank;
            }
            using var ctx = new UserActivityContext();
            ctx.UserActivity.Add(o);
            ctx.SaveChanges(true);
            LoadUserActivity();
        }
        public static void InsertUbiInfo(UbiInfo o)
        {
            if (Leaderboard.FirstOrDefault(w=>w.ID_User==o.Discord_Id) == null)
            {
                InsertLeaderboard(new Leaderboard { ID_User = o.Discord_Id });
            }
            using var ctx = new UbiInfoContext();
            ctx.UbiInfo.Add(o);
            ctx.SaveChanges(true);
            LoadUbiInfo();
        }

        public static void InsertLeaderboard(Leaderboard o)
        {
            using var ctx = new LeaderboardContext();
            ctx.Leaderboard.Add(o);
            ctx.SaveChanges();
            LoadLeaderboard();
        }

        public static int InsertServerRanks(ServerRanks o)
        {
            if (Leaderboard.FirstOrDefault(w => w.ID_User == o.User_ID) == null)
            {
                InsertLeaderboard(new Leaderboard { ID_User = o.User_ID });
            }
            if (ServerSettings.FirstOrDefault(w => w.ID_Server == o.Server_ID) == null)
            {
                InsertServerSettings(new ServerSettings { ID_Server = o.Server_ID });
            }
            using var ctx = new ServerRanksContext();
            ctx.ServerRanks.Add(o);
            ctx.SaveChanges();
            LoadServerRanks();

            return o.ID_Server_Rank;
        }

        public static void InsertWarnings(Warnings o)
        {
            if (ServerRanks.FirstOrDefault(w => w.User_ID == o.User_ID && w.Server_ID == o.Server_ID) == null)
            {
                o.Server_Ranks_ID = InsertServerRanks(new ServerRanks { User_ID = o.User_ID, Server_ID = o.Server_ID });
            }
            else
            {
                o.Server_Ranks_ID = ServerRanks.FirstOrDefault(w => w.User_ID == o.User_ID && w.Server_ID == o.Server_ID).ID_Server_Rank;
            }
            using var ctx = new WarningsContext();
            ctx.Warnings.Add(o);
            ctx.SaveChanges();
            LoadWarnings();
        }

        public static void InsertServerSettings(ServerSettings o)
        {
            using var ctx = new ServerSettingsContext();
            ctx.ServerSettings.Add(o);
            ctx.SaveChanges();
            LoadServerSettings();
        }

        public static void InsertRankRoles(RankRoles o)
        {
            if (ServerSettings.FirstOrDefault(w => w.ID_Server == o.Server_ID) == null)
            {
                InsertServerSettings(new ServerSettings { ID_Server = o.Server_ID });
            }
            using var ctx = new RankRolesContext();
            ctx.RankRoles.Add(o);
            ctx.SaveChanges();
            LoadRankRoles();
        }

        public static void InsertBannedWords(AMBannedWords o)
        {
            if (ServerSettings.FirstOrDefault(w => w.ID_Server == o.Server_ID) == null)
            {
                InsertServerSettings(new ServerSettings { ID_Server = o.Server_ID });
            }
            using var ctx = new AMBannedWordsContext();
            ctx.AMBannedWords.Add(o);
            ctx.SaveChanges();
            LoadRankRoles();
        }

        public static void InsertBotOutputList(BotOutputList o)
        {
            using var ctx = new BotOutputListContext();
            ctx.BotOutputList.Add(o);
            ctx.SaveChanges();
            LoadRankRoles();
        }

        public static long InsertModMail(ModMail o)
        {
            if (ServerRanks.FirstOrDefault(w => w.User_ID == o.User_ID && w.Server_ID == o.Server_ID) == null)
            {
                o.Server_Ranks_ID = InsertServerRanks(new ServerRanks { User_ID = o.User_ID, Server_ID = o.Server_ID });
            }
            else
            {
                o.Server_Ranks_ID = ServerRanks.FirstOrDefault(w => w.User_ID == o.User_ID && w.Server_ID == o.Server_ID).ID_Server_Rank;
            }
            using var ctx = new ModMailContext();
            ctx.ModMail.Add(o);
            ctx.SaveChanges();
            LoadModMail();
            return o.ID;
        }

        public static void InsertRoleTagSettings(RoleTagSettings o)
        {
            if (ServerSettings.FirstOrDefault(w => w.ID_Server == o.Server_ID) == null)
            {
                InsertServerSettings(new ServerSettings { ID_Server = o.Server_ID });
            }
            using var ctx = new RoleTagSettingsContext();
            ctx.RoleTagSettings.Add(o);
            ctx.SaveChanges();
            LoadRoleTagSettings();
        }

        #endregion Insert Functions

        #region Delete Functions

        public static void DeleteUbiInfo(params UbiInfo[] o)
        {
            using var ctx = new UbiInfoContext();
            ctx.UbiInfo.RemoveRange(o);
            ctx.SaveChanges();
            LoadUbiInfo();
        }

        #endregion Delete Functions
    }
}