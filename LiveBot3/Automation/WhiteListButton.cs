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
        /*
        if (e.Interaction.Data.CustomId != "Activate") return;
        DiscordInteractionResponseBuilder responseBuilder = new()
        {
            IsEphemeral = true
        };
        if (await _dbContext.WhiteList.AnyAsync(x=>x.DiscordId==e.User.Id))
        {
            responseBuilder.WithContent("You are already verified.");
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, responseBuilder);
            return;
        }
        
        var member = (DiscordMember)e.User;
        
        Whitelist entry = await _dbContext.WhiteList.FirstOrDefaultAsync(x => x.Username == member.Username || x.Username == member.Nickname);
        
        if (entry == null)
        {
            client.Logger.LogDebug("User not found in the whitelist");
            responseBuilder.WithContent("Your username/Nickname has not been found in the database, please make sure you have set it exactly as on Ubisoft Connect!");
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, responseBuilder);
            return;
        }
        
        if (entry.DiscordId != null)
        {
            client.Logger.LogDebug("username already linked");
            responseBuilder.WithContent("This username has already been linked, please make sure you have set your username/nickname as your Ubisoft Connect name");
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, responseBuilder);
            return;
        }

        ulong? whiteListRole = _dbContext.Guilds.First(x => x.Id == e.Guild.Id).WhiteListRoleId;
        if (whiteListRole == null)
        {
            client.Logger.LogDebug("No role specified to be assigned, exiting.");
            return;
        }

        DiscordRole role = e.Guild.GetRole((ulong)whiteListRole);

            await member.GrantRoleAsync(role);

        entry.DiscordId = member.Id;
        _dbContext.Update(entry);
        await _dbContext.SaveChangesAsync();
        responseBuilder.WithContent("You have verified successfully!");
        await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, responseBuilder);
        //*/
    }
}