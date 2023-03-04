using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    public class RankRoles
    {
        public RankRoles(ulong guildId, ulong roleId,long serverRank)
        {
            GuildId = guildId;
            RoleId = roleId;
            ServerRank = serverRank;
        }
        public int Id { get; set; }
        public ulong GuildId
        { 
            get => _guildId; 
            set => _guildId = Convert.ToUInt64(value);
        }

        private ulong _guildId;
        public ulong RoleId
        { 
            get => _roleId;
            set => _roleId = Convert.ToUInt64(value);
        }

        private ulong _roleId;
        public long ServerRank { get; set; }
        
        public Guild Guild { get; set; }
    }
}