using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    public class StreamNotifications
    {
        public StreamNotifications(ulong guildId)
        {
            GuildId = guildId;
        }
        public int Id { get; set; }
        public ulong GuildId
        { 
            get => _guildId; 
            set => _guildId = Convert.ToUInt64(value);
        }

        private ulong _guildId;
        public string[] Games { get; set; }
        public ulong[] RoleIds
        { 
            get => _roleIds; 
            set => _roleIds = value.Select(Convert.ToUInt64).ToArray();
        }

        private ulong[] _roleIds;
        public ulong ChannelId
        { 
            get => _channelId;
            set => _channelId = Convert.ToUInt64(value);
        }

        private ulong _channelId;
        public Guild Guild { get; set; }
    }
}