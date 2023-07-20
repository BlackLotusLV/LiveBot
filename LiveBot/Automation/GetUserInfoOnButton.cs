using LiveBot.DB;
using LiveBot.Services;

namespace LiveBot.Automation;

public class GetUserInfoOnButton
{
    private readonly IWarningService _warningService;

    public GetUserInfoOnButton(IWarningService warningService)
    {
        _warningService = warningService;
    }

    public async Task OnPress(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        if (e.Interaction is not { Type: InteractionType.Component, User.IsBot: false }|| !e.Interaction.Data.CustomId.Contains(_warningService.UserInfoButtonPrefix) || e.Interaction.Guild == null) return;
        string idString = e.Interaction.Data.CustomId.Replace(_warningService.UserInfoButtonPrefix, "");
        if(!ulong.TryParse(idString,out ulong userId)) return;
        DiscordUser user = await client.GetUserAsync(userId);
        DiscordEmbed embed = await _warningService.GetUserInfoAsync(e.Guild, user);
        DiscordInteractionResponseBuilder response = new()
        {
            IsEphemeral = true
        };
        response.AddEmbed(embed);
        await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, response);
    }
}