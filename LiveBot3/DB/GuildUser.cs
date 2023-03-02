using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Server_Ranks", Schema = "livebot")]
    public class GuildUser
    {
        public GuildUser(LiveBotDbContext context, ulong userDiscordId,ulong guildId)
        {
            UserDiscordId = userDiscordId;
            GuildId = guildId;
            User user = context.Users.FirstOrDefault(x => x.DiscordId == this.UserDiscordId);
            Guild guild = context.Guilds.FirstOrDefault(x => x.Id == this.GuildId);
            if (user == null)
            {
                user = new User(context, UserDiscordId);
                context.Users.Add(user);
            }

            if (guild != null) return;
            guild = new Guild() { Id = this.GuildId };
            context.Guilds.Add(guild);
        }
        public ulong UserDiscordId
        { 
            get => _userDiscordId;
            set => _userDiscordId = Convert.ToUInt64(value);
        }

        private ulong _userDiscordId;
        public ulong GuildId
        { 
            get => _guildId;
            set => _guildId = Convert.ToUInt64(value);
        }

        private ulong _guildId;
        public int KickCount { get; set; }
        public int BanCount { get; set; }
        public bool IsModMailBlocked { get; set; }
        
        public User User { get; set; }
        public Guild Guild { get; set; }
        
        public virtual ICollection<ModMail> ModMails { get; set; }
        public virtual ICollection<UserActivity> UserActivity { get; set; }
        public virtual ICollection<Infraction> Infractions { get; set; }
    }
}