using LiveBot.DB;
using LiveBot.Services;

namespace LiveBot.Automation
{
    internal  class MemberFlow
    {
        private readonly IDbContextFactory _dbContextFactory;
        private readonly IModMailService _modMailService;
        private readonly IDatabaseMethodService _databaseMethodService;

        public MemberFlow(IModMailService modMailService, IDbContextFactory dbContextFactory, IDatabaseMethodService databaseMethodService)
        {
            _modMailService = modMailService;
            _dbContextFactory = dbContextFactory;
            _databaseMethodService = databaseMethodService;
        }
        public async Task Welcome_Member(DiscordClient client, GuildMemberAddEventArgs e)
        {
            await using LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
            Guild guild = await liveBotDbContext.Guilds.FindAsync(e.Guild.Id) ?? await _databaseMethodService.AddGuildAsync(new Guild(e.Guild.Id));
            if (guild?.WelcomeChannelId == null || guild.HasScreening) return;
            DiscordChannel welcomeChannel = e.Guild.GetChannel(Convert.ToUInt64(guild.WelcomeChannelId));

            if (guild.WelcomeMessage == null) return;
            string msg = guild.WelcomeMessage;
            msg = msg.Replace("$Mention", $"{e.Member.Mention}");
            await welcomeChannel.SendMessageAsync(msg);

            if (guild.RoleId==null) return;
            DiscordRole role = e.Guild.GetRole(Convert.ToUInt64(guild.RoleId));
            await e.Member.GrantRoleAsync(role);
        }

        public async Task Say_Goodbye(DiscordClient client, GuildMemberRemoveEventArgs e)
        {
            await using LiveBotDbContext liveBotDbContext = _dbContextFactory.CreateDbContext();
            Guild guild = await liveBotDbContext.Guilds.FindAsync(e.Guild.Id) ?? await _databaseMethodService.AddGuildAsync(new Guild(e.Guild.Id));
            bool pendingCheck = guild != null && !(guild.HasScreening && e.Member.IsPending == true);
            if (guild is { WelcomeChannelId: not null } && pendingCheck)
            {
                DiscordChannel welcomeChannel = e.Guild.GetChannel(Convert.ToUInt64(guild.WelcomeChannelId));
                if (guild.GoodbyeMessage != null)
                {
                    string msg = guild.GoodbyeMessage;
                    msg = msg.Replace("$Username", $"{e.Member.Username}");
                    await welcomeChannel.SendMessageAsync(msg);
                }
            }
            DB.ModMail modMailEntry = liveBotDbContext.ModMail.FirstOrDefault(w => w.UserDiscordId == e.Member.Id && w.GuildId == e.Guild.Id && w.IsActive);
            if (modMailEntry != null)
            {
                await _modMailService.CloseModMailAsync(client,modMailEntry, (DiscordUser)e.Member, "Mod Mail entry closed due to user leaving", "**Mod Mail closed!\n----------------------------------------------------**");
            }
        }
    }
}