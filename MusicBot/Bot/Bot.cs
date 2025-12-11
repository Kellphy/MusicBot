using DiscordBot.Commands;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using MusicBot.Events;
using MusicBot.Models;
using MusicBot.Services;
using System;

namespace DiscordBot
{
    /// <summary>
    /// Main bot class that initializes Discord client and commands
    /// </summary>
    public class Bot
    {
        public DiscordClient Client { get; private set; }
        public CommandsNextExtension Commands { get; private set; }

        private readonly IServiceProvider _services;
        private readonly ILoggingService _loggingService;
        private readonly IConfigurationService _configurationService;
        private readonly IShortcutService _shortcutService;
        private static IServiceProvider _staticServiceProvider;

        /// <summary>
        /// Gets the service provider (for backward compatibility)
        /// </summary>
        public static IServiceProvider GetServiceProvider() => _staticServiceProvider;

        public Bot(
            DiscordClient discordClient,
            IServiceProvider services,
            ILoggingService loggingService,
            IConfigurationService configurationService,
            IShortcutService shortcutService)
        {
            _services = services;
            _loggingService = loggingService;
            _configurationService = configurationService;
            _shortcutService = shortcutService;
            _staticServiceProvider = services;
            Client = discordClient;

            loggingService.ShowBanner();
            loggingService.LogInfo($"Version: {BotConstants.Version}");

            _shortcutService.LoadShortcuts();
            SetupInteractivity();
            RegisterCommands();
            
            Client.ConnectAsync();
            new DiscordEvents().EventsFeedback(Client);

            _loggingService.LogInfo("Bot initialized successfully");
        }

        private void SetupInteractivity()
        {
            Client.UseInteractivity(new InteractivityConfiguration
            {
                Timeout = TimeSpan.FromMinutes(5)
            });
        }

        private void RegisterCommands()
        {
            var commandsConfig = new CommandsNextConfiguration
            {
                StringPrefixes = new[] { _configurationService.Configuration.Prefix },
                EnableDms = false,
                EnableMentionPrefix = true,
                DmHelp = false,
                EnableDefaultHelp = false,
                Services = _services,
            };

            Commands = Client.UseCommandsNext(commandsConfig);
            
            var slashCommands = Client.UseSlashCommands(new SlashCommandsConfiguration { Services = _services });
            slashCommands.RegisterCommands<VoiceSlashCommands>();
        }
    }
}