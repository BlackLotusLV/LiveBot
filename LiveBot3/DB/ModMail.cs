using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    public class ModMail
    {
        public ModMail(LiveBotDbContext context, ulong guildId, ulong userDiscordId, DateTime lastMessageTime, string colorHex)
        {
            GuildId = guildId;
            UserDiscordId = userDiscordId;
            LastMessageTime = lastMessageTime;
            ColorHex = colorHex;
        }
        private ulong _guildId;
        private ulong _userDiscordId;
        
        public long Id { get; set; }
        
        public ulong GuildId
        { 
            get => _guildId;
            set => _guildId = Convert.ToUInt64(value);
        }

        public ulong UserDiscordId
        { 
            get => _userDiscordId;
            set => _userDiscordId = Convert.ToUInt64(value);
        }
        public DateTime LastMessageTime { get; set; }
        public bool HasChatted { get; set; }
        public bool IsActive { get; set; }
        public string ColorHex { get; set; }
        
        public GuildUser GuildUser { get; set; }
    }
}