using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LiveBot.Services;

namespace LiveBot.DB
{
    public class Infraction
    {
        public Infraction(ulong adminDiscordId, ulong userId, ulong guildId, string reason, bool isActive, InfractionType infractionType)
        {
            UserId = userId;
            GuildId = guildId;
            Reason = reason;
            IsActive = isActive;
            AdminDiscordId = adminDiscordId;
            InfractionType = infractionType;
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
        public DateTimeOffset TimeCreated { get; set; } = DateTimeOffset.UtcNow;

        public ulong AdminDiscordId
        {
            get => _adminDiscordId;
            set => _adminDiscordId = Convert.ToUInt64(value);
        }
        public InfractionType InfractionType { get; set; }
        public GuildUser GuildUser { get; set; }
    }

    public enum InfractionType
    {
        Warning,
        Kick,
        Ban,
        Note,
        TimeoutAdded,
        TimeoutRemoved,
        TimeoutExtended,
        TimeoutReduced
    }
}