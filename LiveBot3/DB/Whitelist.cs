using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveBot.DB;

[Table("Whitelist", Schema = "livebot")]
public class Whitelist
{
    
    [Key]
    [Column("id")]
    public Guid Id { get; set; }
    
    [Column("username")] public string Username { get; set; }
    [Column("discord_id")] public ulong? DiscordId 
    { 
        get =>_discordId;
        set => _discordId = Convert.ToUInt64(value);
    }
    private ulong? _discordId;
}