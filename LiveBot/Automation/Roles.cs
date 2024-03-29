﻿using LiveBot.DB;
using LiveBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LiveBot.Automation;

internal sealed class Roles
{
    private readonly IDbContextFactory<LiveBotDbContext> _dbContextFactory;

    public Roles(IDbContextFactory<LiveBotDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task Button_Roles(object client, ComponentInteractionCreateEventArgs e)
    {
        if (e.Interaction is not { Type: InteractionType.Component, User.IsBot: false }|| !e.Interaction.Data.CustomId.Contains("ButtonRole-") || e.Interaction.Guild == null) return;
        await using LiveBotDbContext liveBotDbContext = await _dbContextFactory.CreateDbContextAsync();
        var rolesList = await liveBotDbContext.ButtonRoles.Where(x => x.GuildId == e.Interaction.GuildId && x.ChannelId == e.Interaction.ChannelId).ToListAsync();
        if (rolesList.Count == 0) return;
        string buttonCustomId = e.Interaction.Data.CustomId.Replace("ButtonRole-","");
        if (!ulong.TryParse(buttonCustomId,out ulong roleId)) return;
        var buttonRoleInfo = rolesList.Where(roles => e.Interaction.Guild.Roles.Any(guildRole => Convert.ToUInt64(roles.ButtonId) == guildRole.Value.Id)).ToList();
        if (buttonRoleInfo.Count > 0 && buttonRoleInfo[0].ChannelId == e.Interaction.Channel.Id)
        {
            DiscordInteractionResponseBuilder response = new()
            {
                IsEphemeral = true
            };
            var member = e.Interaction.User as DiscordMember;
            DiscordRole role = e.Interaction.Guild.Roles.FirstOrDefault(w => w.Value.Id == roleId).Value;
            if (member == null) return;
            if (member.Roles.Any(w => w.Id == roleId))
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