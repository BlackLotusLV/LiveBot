using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    public class Infraction
    {
        public Infraction(LiveBotDbContext context, ulong adminDiscordId, string reason, bool isActive, string type)
        {
            Reason = reason;
            IsActive = isActive;
            AdminDiscordId = adminDiscordId;
            Type = type;
        }
        private ulong _guildId;
        private ulong _userId;
        private ulong _adminDiscordId;
        
        public long Id { get; set; }
        
        public ulong UserId
        {
            get => _userId;
            set => _userId = Convert.ToUInt64(value);
        }

        public ulong GuildId
        {
            get => _guildId;
            set => _guildId = Convert.ToUInt64(value);
        }
        
        public string Reason { get; set; }
        public bool IsActive { get; set; }
        public DateTime TimeCreated { get; set; }

        public ulong AdminDiscordId
        {
            get => _adminDiscordId;
            set => _adminDiscordId = Convert.ToUInt64(value);
        }

        public string Type { get; set; }
        public GuildUser GuildUser { get; set; }
    }
}