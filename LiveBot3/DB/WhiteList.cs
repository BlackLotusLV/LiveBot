namespace LiveBot.DB;

public class WhiteList
{
    public long Id { get; set; }
    
    public string UbisoftName { get; set; }
    private ulong? _discordId;

    public ulong? DiscordId
    {
        get=>_discordId; 
        set=> _discordId=Convert.ToUInt64(value);
    }
    public int WhiteListSettingsId { get; set; }
    
    public WhiteListSettings Settings { get; set; }
}