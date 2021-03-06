﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;


namespace DIBot.Services
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IServiceProvider _provider;


        /// <summary>
        /// Creates a new <see cref="CommandHandler"/>.
        /// </summary>
        /// <param name="discord">The Discord socket client to use.</param>
        /// <param name="commands">The command service to use.</param>
        /// <param name="provider">The service provider to use.</param>
        /// <param name="config">The config to use.</param>
        public CommandHandler(
            DiscordSocketClient discord,
            CommandService commands,
            IServiceProvider provider
        )
        {
            _discord = discord;
            _commands = commands;
            _provider = provider;

            _discord.MessageReceived += OnMessageReceivedAsync;
        }


        /// <summary>
        /// Handles the given message.
        /// </summary>
        /// <param name="socketMessage">The socket message.</param>
        /// <returns>An awaitable task.</returns>
        private async Task OnMessageReceivedAsync(SocketMessage socketMessage)
        {
            // Ensure the message is from a user/bot.
            var message = socketMessage as SocketUserMessage;

            // If the message is null, return.
            if (message == null)
            {
                return;
            }

            // Ignore self when checking commands.
            if (message.Author == _discord.CurrentUser)
            {
                return;
            }
            
            // Create the command context.
            var context = new SocketCommandContext(_discord, message);

            int argPos = 0;

            // Check if the message has a valid command prefix.
            if (message.HasStringPrefix(ConfigUtil.Config.Prefix, ref argPos) || message.HasMentionPrefix(_discord.CurrentUser, ref argPos))
            {
                // Look at the second character of the command, if it's the same as the prefix then ignore it.
                // This gets around the bot treating a message like '...' as a command and trying to process it.
                if (!string.Equals(message.Content.Substring(1, 1), ConfigUtil.Config.Prefix))
                {
                    var command = message.Content.Split(' ')[0].Substring(1).ToLowerInvariant();

                    if (message.Author.Id != 178245652633878528 && 
                        !ConfigUtil.Config.Permissions.HasPermission(message.Author, command, context.Guild)
                    )
                    {
                        return;
                    }

                    Console.WriteLine(message.Content);

                    // Execute the command.
                    var result = await _commands.ExecuteAsync(context, argPos, _provider);

                    if (!result.IsSuccess)
                    {
                        /*
                        EmbedBuilder embed;
                        
                        embed = new EmbedBuilder();
                        embed.WithColor(Color.DarkRed);
                        embed.AddField(":warning: An unexpected error occurred.", $"The command: '{message.Content}' is not a registered command.");

                        // If not successful, reply with the error.
                        await context.Channel.SendMessageAsync("", embed: embed.Build());
                        */
                    }
                }
            }
        }
    }
}
