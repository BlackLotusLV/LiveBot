using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    public class RoleTagSettings
    {
        public RoleTagSettings(ulong guildId)
        {
            GuildId = guildId;
        }
        private ulong _guildId;
        private ulong _roleId;
        private ulong _channelId;
        private ulong _emojiId;
        public long Id { get; set; }
        public ulong GuildId
        { 
            get => _guildId; 
            set => _guildId = Convert.ToUInt64(value);
        }
        public ulong RoleId
        { 
            get => _roleId;
            set => _roleId = Convert.ToUInt64(value);
        }
        public ulong ChannelId
        { 
            get => _channelId; 
            set => _channelId = Convert.ToUInt64(value);
        }
        public int Cooldown { get; set; }
        public DateTime LastTimeUsed { get; set; }
        public ulong EmojiId
        { 
            get => _emojiId;
            set => _emojiId = Convert.ToUInt64(value);
        }
        public string Message { get; set; }
        public string Description { get; set; }
        
        public Guild Guild { get; set; }
    }
}