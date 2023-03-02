using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB
{
    public class UbiInfo
    {
        public UbiInfo(LiveBotDbContext context, ulong userDiscordId)
        {
            UserDiscordId = userDiscordId;
            User user = context.Users.FirstOrDefault(x => x.DiscordId == this.UserDiscordId);
            if (user != null) return;
            user = new User(context, UserDiscordId) { DiscordId = this.UserDiscordId };
            context.Users.Add(user);
        }
        public int Id { get; set; }
        public ulong UserDiscordId
        {
            get => _userDiscordId; 
            init => _userDiscordId = Convert.ToUInt64(value);
        }

        private readonly ulong _userDiscordId;
        public string ProfileId { get; set; }
        public string Platform { get; set; }
        
        public User User { get; set; }
    }
}
