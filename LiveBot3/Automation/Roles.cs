using LiveBot.DB;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation
{
    internal class Roles
    {
        private readonly LiveBotDbContext _dbContext;
        public Roles(LiveBotDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task Button_Roles(object client, ComponentInteractionCreateEventArgs e)
        {
            if (e.Interaction is { Type: InteractionType.Component, User.IsBot: false } && e.Interaction.Guild != null)
            {
                var buttonRoleInfo = await _dbContext.ButtonRoles
                    .Where(w => w.Server_ID == e.Interaction.GuildId && w.Channel_ID == e.Interaction.ChannelId && e.Interaction.Guild.Roles.Any(f => f.Value.Id == Convert.ToUInt64(w.Button_ID)))
                    .ToListAsync();
                if (buttonRoleInfo.Count > 0 && buttonRoleInfo[0].Channel_ID == e.Interaction.Channel.Id)
                {
                    DiscordInteractionResponseBuilder response = new()
                    {
                        IsEphemeral = true
                    };
                    DiscordMember member = e.Interaction.User as DiscordMember;
                    DiscordRole role = e.Interaction.Guild.Roles.FirstOrDefault(w => w.Value.Id == Convert.ToUInt64(e.Interaction.Data.CustomId)).Value;
                    if (member==null) return;
                    if (member.Roles.Any(w => w.Id == Convert.ToUInt64(e.Interaction.Data.CustomId)))
                    {
                        await member.RevokeRoleAsync(role);
                        response.Content = $"{member.Mention} the {role.Mention} role has been removed.";
                    }
                    else
                    {
                        await member.GrantRoleAsync(role);
                        response.Content = $"{member.Mention} you have been given the {role.Mention} role.";
                    }
                    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, response);
                }
            }
        }
    }
}