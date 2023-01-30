using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    [Table("Server_Ranks", Schema = "livebot")]
    public class ServerRanks
    {
        public ServerRanks(LiveBotDbContext context, ulong userDiscordId,ulong guildId)
        {
            UserDiscordId = userDiscordId;
            GuildId = guildId;
            Leaderboard leaderboard = context.Leaderboard.FirstOrDefault(x => x.UserDiscordId == this.UserDiscordId);
            ServerSettings serverSettings = context.ServerSettings.FirstOrDefault(x => x.GuildId == this.GuildId);
            if (leaderboard == null)
            {
                leaderboard = new Leaderboard(context, UserDiscordId);
                context.Leaderboard.Add(leaderboard);
            }

            if (serverSettings != null) return;
            serverSettings = new ServerSettings() { GuildId = this.GuildId };
            context.ServerSettings.Add(serverSettings);
        }
        [Key]
        [Column("id_server_rank")]
        public int IdServerRank { get; set; }

        [Required]
        [Column("user_id")]
        public ulong UserDiscordId
        { 
            get => _userDiscordId;
            set => _userDiscordId = Convert.ToUInt64(value);
        }

        private ulong _userDiscordId;

        [Required]
        [Column("server_id")]
        public ulong GuildId
        { 
            get => _guildId;
            set => _guildId = Convert.ToUInt64(value);
        }

        private ulong _guildId;

        [Required]
        [Column("kick_count")]
        public int KickCount { get; set; }

        [Required]
        [Column("ban_count")]
        public int BanCount { get; set; }

        [Required]
        [Column("mm_blocked")]
        public bool IsModMailBlocked { get; set; }
        
        public virtual ICollection<ModMail> ModMail { get; set; }
        public virtual ICollection<UserActivity> UserActivity { get; set; }
        public virtual ICollection<Warnings> Warnings { get; set; }
    }
}