namespace LiveBot.DB;

public class PhotoCompSettings
{
    public PhotoCompSettings(ulong guildId)
    {
        GuildId = guildId;
    }
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public int WinnerCount { get; set; }
    public int MaxEntries { get; set; }
    public int CustomParameter { get; set; }
    public string CustomName { get; set; }
    public ulong DumpChannelId { get; set; }
    public bool IsOpen { get; set; }
    
    public ICollection<PhotoCompEntries> Entries { get; set; }
    public Guild Guild { get; set; }
}