using LiveBot.DB;
using LiveBot.Services;

namespace LiveBot.Automation;

public class GetInfractionOnButton
{
    
    private readonly IWarningService _warningService;
    private readonly LiveBotDbContext _databaseContext;

    public GetInfractionOnButton(IWarningService warningService, LiveBotDbContext databaseContext)
    {
        _warningService = warningService;
        _databaseContext = databaseContext;
    }

    public async Task OnPress(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        if (e.Interaction is not { Type: InteractionType.Component, User.IsBot: false }|| !e.Interaction.Data.CustomId.Contains("GetInfractions-") || e.Interaction.Guild == null) return;
        string idString = e.Interaction.Data.CustomId.Replace("GetInfractions-", "");
        if(!ulong.TryParse(idString,out ulong userId)) return;
        DiscordUser user = await client.GetUserAsync(userId);
        DiscordEmbed embed = await _warningService.GetUserWarningsAsync(e.Guild, user, true);
        DiscordInteractionResponseBuilder response = new()
        {
            IsEphemeral = true
        };
        response.AddEmbed(embed);
        await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, response);
    }
}