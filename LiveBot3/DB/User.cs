using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    public sealed class User
    {
        public User(ulong discordId)
        {
            DiscordId = discordId;
        }
        public ulong DiscordId
        { 
            get => _discordId; 
            set => _discordId = Convert.ToUInt64(value);
        }
        private ulong _discordId;
        public int CookiesGiven { get; set; }
        public int CookiesTaken { get; set; }
        public DateTime CookieDate { get; set; }
        public string Locale { get; set; }
        public ulong? ParentDiscordId
        {
            get => _parentDiscordId;
            set => _parentDiscordId =  value.HasValue ? Convert.ToUInt64(value) :default(ulong?);
        }
        private ulong? _parentDiscordId;
        
        public User Parent { get; set; }
        
        public ICollection<UbiInfo> UbiInfo { get; set; }
        public ICollection<User> ChildUsers { get; set; }
        public ICollection<GuildUser> UserGuilds { get; set; }
    }
}