using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Timer = System.Timers.Timer;
using DIBot.Services;
using System;

namespace DIBot
{
    class Program
    {
        public static IServiceProvider ServiceProvider;

        private CommandService _commands;
        private DiscordSocketClient _client;
        private CalendarScheduler _calendar;
        private MasterDivisionRegistry _mdr;

        public static void Main(string[] args)
            => new Program().StartAsync().GetAwaiter().GetResult();
        
        public async Task StartAsync()
        {
            try
            {
                ConfigUtil.Load();
            }
            catch(Exception e)
            {
                // Catch the error and show a dialog. The error will be logged when trying to load the config
                // file anyway, so all we need to do here is to ensure that the user knows why the application crashed.
                MessageBox.Show(
                    "An error occurred whilst trying to load the configuration file. Please ensure the configuration file exists and is named correctly.",
                    "Invalid Configuration File",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }

            _commands = new CommandService(
                new CommandServiceConfig
                {
                    DefaultRunMode = RunMode.Async,
                    LogLevel = LogSeverity.Verbose
                }
            );

            _client = new DiscordSocketClient(
                new DiscordSocketConfig
                {
                    LogLevel = LogSeverity.Verbose,
                    MessageCacheSize = 1000
                }
            );

            _mdr = MasterDivisionRegistry.Load() ?? new MasterDivisionRegistry();

            _calendar = new CalendarScheduler(_client, _mdr);

            // Add singletons of all the services we will need.
            var services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .AddSingleton(_mdr)
                .AddSingleton(_calendar)
                .AddSingleton<CommandHandler>()
                .AddSingleton<LoggingService>()
                .AddSingleton<StartupService>();

            // Create the service provider.
            ServiceProvider = new DefaultServiceProviderFactory().CreateServiceProvider(services);

            // Initialize all the services.
            ServiceProvider.GetRequiredService<LoggingService>();
            await ServiceProvider.GetRequiredService<StartupService>().StartAsync();
            ServiceProvider.GetRequiredService<CommandHandler>();

            // Prevent the application from closing.
            await Task.Delay(-1);
        }
    }
}
