using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    public class RankRoles
    {
        public RankRoles(LiveBotDbContext context)
        {
            Guild guild = context.Guilds.FirstOrDefault(x => x.Id == this.GuildId);
            if (guild != null) return;
            guild = new Guild() { Id = this.GuildId };
            context.Guilds.Add(guild);
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