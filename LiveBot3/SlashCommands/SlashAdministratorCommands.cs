using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;

namespace LiveBot.SlashCommands
{
    [SlashCommandGroup("Admin","Administrator commands.", false)]
    [SlashRequireGuild]
    [SlashRequireBotPermissions(Permissions.ManageGuild)]
    internal class SlashAdministratorCommands : ApplicationCommandModule
    {
        [SlashCommand("Say","Bot says a something")]
        public async Task Say(InteractionContext ctx, [Option("Message", "The message what the bot should say.")] string message, [Option("Channel", "Channel where to send the message")] DiscordChannel channel = null)
        {
            await ctx.DeferAsync(true);
            if (channel==null)
            {
                channel = ctx.Channel;
            }
            await channel.SendMessageAsync(message);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Message has been sent"));
        }

        [SlashCommand("Button-Message", "Creates a button message")]
        public async Task ButtonMessage(InteractionContext ctx, [Choice("One", 1)][Choice("Two", 2)][Choice("Three", 3)][Choice("Four", 4)][Option("Button-Count","How many buttons to have")] long count)
        {
            string customID = $"Button-Creator-{ctx.User.Id}";
            var modal = new DiscordInteractionResponseBuilder().WithTitle("Button Message Editor").WithCustomId(customID)
                .AddComponents(new TextInputComponent("Base Message", "base", null, null, true, TextInputStyle.Paragraph));
            for (int i = 0; i < count; i++)
            {
                
                modal.AddComponents(new TextInputComponent($"Button {i + 1} ", $"button{i + 1}-info","id|Label|emojiID", null, true, TextInputStyle.Short));
            }
            await ctx.CreateResponseAsync(InteractionResponseType.Modal, modal);

            var interactivity = ctx.Client.GetInteractivity();
            var response = await interactivity.WaitForModalAsync(customID, ctx.User);
            if (!response.TimedOut)
            {
                DiscordButtonComponent[] buttons = new DiscordButtonComponent[count];
                for (int i = 0; i < count; i++)
                {
                    string[] buttonInfo = response.Result.Values[$"button{i + 1}-info"].Split('|');
                    buttons[i] = new DiscordButtonComponent(ButtonStyle.Primary,buttonInfo[0], buttonInfo[1],false, UInt64.TryParse(buttonInfo[2], out ulong emojiID) ? new DiscordComponentEmoji(emojiID) : null);
                }

                DiscordMessageBuilder message = new()
                {
                    Content = response.Result.Values["base"]
                };
                message.AddComponents(buttons);
                await message.SendAsync(ctx.Channel);
                await response.Result.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Button Message created").AsEphemeral());
            }
        }
    }
}
