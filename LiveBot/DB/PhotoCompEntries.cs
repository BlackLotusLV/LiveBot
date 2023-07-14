namespace LiveBot.DB;

public class PhotoCompEntries
{
    public PhotoCompEntries(ulong userId, int competitionId, string imageUrl, DateTime dateSubmitted)
    {
        UserId = userId;
        CompetitionId = competitionId;
        ImageUrl = imageUrl;
        DateSubmitted = dateSubmitted;
    }
    public long Id { get; set; }
    public ulong UserId { get; set; }
    public int CompetitionId { get; set; }
    public string ImageUrl { get; set; }
    public DateTime DateSubmitted { get; set; }
    
    public PhotoCompSettings Competition { get; set; }
    public User User { get; set; }
    
}