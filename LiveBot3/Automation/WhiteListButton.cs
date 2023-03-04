using LiveBot.DB;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation;

public class WhiteListButton
{
    private readonly LiveBotDbContext _dbContext;
    public WhiteListButton(LiveBotDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task Activate(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        if (e.Guild == null ||e.Interaction.Type != InteractionType.Component || e.Interaction.Data.CustomId != "Activate") return;
        DiscordInteractionResponseBuilder responseBuilder = new() 
        {
            IsEphemeral = true
        };
        var settingsList = await _dbContext.WhiteListSettings.Where(x => x.GuildId == e.Guild.Id).ToListAsync();
        if (settingsList.Count==0)
        {
            responseBuilder.WithContent("Whitelist feature not set up properly. Contact a moderator.");
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, responseBuilder);
            return;
        }
        var member = (DiscordMember)e.User;
        var entries = await _dbContext.WhiteLists.Where(x => x.UbisoftName == member.Username || x.UbisoftName == member.Nickname).ToListAsync();
        WhiteList entry = entries.FirstOrDefault(whiteList => settingsList.Any(wls => wls.Id == whiteList.WhiteListSettingsId));

        if (entry==null)
        {
            responseBuilder.WithContent("Your username/Nickname has not been found in the database, please make sure you have set it exactly as on Ubisoft Connect!");
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, responseBuilder);
            return;
        }

        if (entry.DiscordId !=null)
        {
            responseBuilder.WithContent("You have already been verified once, if you think this is a mistake please contact a moderator");
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, responseBuilder);
            return;
        }

        DiscordRole role = e.Guild.GetRole(entry.Settings.RoleId);
        await member.GrantRoleAsync(role);
        entry.DiscordId = member.Id;
        _dbContext.WhiteLists.Update(entry);
        await _dbContext.SaveChangesAsync();
        
        responseBuilder.WithContent("You have verified successfully!");
        await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, responseBuilder);
    }
}