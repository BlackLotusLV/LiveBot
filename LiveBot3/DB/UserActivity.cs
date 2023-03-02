using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    public class UserActivity
    {
        public long Id { get; set; }
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
        public int Points { get; set; }
        public DateTime Date
        { 
            get =>_date; 
            set => _date = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
        private DateTime _date;
        public int ServerRanksId { get; set; }
        
        public GuildUser GuildUser { get; set; }
    }
}