using DiscordBot.Commands;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Lavalink;
using DSharpPlus.Net;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.EventArgs;
using KellphyBot.Events;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class Bot
    {
        public DiscordClient Client { get; private set; }
        public CommandsNextExtension Commands { get; private set; }

        private readonly IServiceProvider _services;

        ConfigJson configJson;
        public Bot(IServiceProvider services)
        {
            DataMethods.SendKellphy();
            //Config.json
            var json = string.Empty;
            using (var fs = File.OpenRead("config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = sr.ReadToEnd();
            configJson = JsonConvert.DeserializeObject<ConfigJson>(json);

            if(configJson.Token.Length < 30)
            {
                DataMethods.SendErrorLogs("I'm pretty sure you forgot to add your token. Be sure to not override it when updating.");
            }
            if (configJson.Prefix.Length > 5)
            {
                DataMethods.SendErrorLogs($"Are you sure you want a prefix that long? It's currently set to: \"{configJson.Prefix}\".");
            }

            var config = new DiscordConfiguration
            {
                Token = configJson.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Error,
                Intents = DiscordIntents.AllUnprivileged,
            };

            Client = new DiscordClient(config);

            Client.Ready += ClientReady;

            //Setup commands
            var commandsConfig = new CommandsNextConfiguration
            {
                StringPrefixes = new string[] { configJson.Prefix },
                EnableDms = false,
                EnableMentionPrefix = true,
                DmHelp = false,
                EnableDefaultHelp = false,
                Services = services,
            };
            Commands = Client.UseCommandsNext(commandsConfig);
            Commands.RegisterCommands<VoiceCommands>();

            var slashConfig = new SlashCommandsConfiguration
            {
                Services = services
            };
            var slash = Client.UseSlashCommands(slashConfig);

            //slash.RegisterCommands<SlashCommands>();

            _services = services.GetService<IServiceProvider>();

            Commands.CommandErrored += OnCommandError;
            Commands.CommandExecuted += OncommandExecute;
            slash.SlashCommandErrored += OnSlashCommandError;
            slash.SlashCommandExecuted += OnSlashCommandExecute;
            //Connect bot
            Client.ConnectAsync();

            Client.GuildDownloadCompleted += Client_GuildDownloadCompleted;
        }

        private Task Client_GuildDownloadCompleted(DiscordClient client, GuildDownloadCompletedEventArgs e)
        {
            _ = Task.Run(() => Status(client));
            ConnectLavaLink();
            return Task.CompletedTask;
        }
        private async Task Status(DiscordClient client)
        {
            ActivityType activityType = ActivityType.Playing;
            while (true)
            {
                var activity = new DiscordActivity($"{configJson.Prefix}help | Local music bot: {CustomStrings.github} | Full bot: kellphy.com/kompanion",activityType);

                await client.UpdateStatusAsync(activity);
                await Task.Delay(TimeSpan.FromMinutes(5));
            }
        }

        async void ConnectLavaLink()
        {
            var endpoint = new ConnectionEndpoint
            {
                Hostname = "127.0.0.1", // From your server configuration.
                Port = 2333 // From your server configuration
            };

            var lavalinkConfig = new LavalinkConfiguration
            {
                Password = "youshallnotpass", // From your server configuration.
                RestEndpoint = endpoint,
                SocketEndpoint = endpoint
            };

            //await Task.Delay(1000 * 5);
            var lavalink = Client.UseLavalink();
            await lavalink.ConnectAsync(lavalinkConfig); // Make sure this is after Discord.ConnectAsync(). 

            DataMethods.SendLogs("Lavalink Connected!");
        }

        private async Task OnSlashCommandExecute(SlashCommandsExtension sender, SlashCommandExecutedEventArgs e)
        {
            DataMethods.SendLogs(new MyContext(e.Context));
            await Task.CompletedTask;
        }
        private async Task OnSlashCommandError(SlashCommandsExtension sender, SlashCommandErrorEventArgs e)
        {
            VoiceCommands voice = new VoiceCommands(_services);
            if (e.Exception is ArgumentException)
            {
                await voice.Help(new MyContext(e.Context), e.Context.CommandName);
            }
            else if (e.Exception is ChecksFailedException)
            {
                DataMethods.SendErrorLogs($"WARNING: {e.Context.User.Username}, you do not have the required permissions or you keep spamming the command.");
            }
            else
            {
                DataMethods.SendErrorLogs($"{e.Context.Guild.Name} | {e.Context.Channel} | {e.Context.User.Username} | Error: {e.Exception}");
            }
        }
        private async Task OncommandExecute(CommandsNextExtension sender, CommandExecutionEventArgs e)
        {
            DataMethods.SendLogs(new MyContext(e.Context));
            await Task.CompletedTask;
        }
        private async Task OnCommandError(CommandsNextExtension sender, CommandErrorEventArgs e)
        {
            if (e.Exception is ArgumentException)
            {
                VoiceCommands voice = new VoiceCommands(_services);
                await voice.Help(new MyContext(e.Context), e.Command.Name);
            }
            else if (e.Exception is ChecksFailedException)
            {
                var firstCheck = e.Command.ExecutionChecks[0];
                if (firstCheck is DSharpPlus.CommandsNext.Attributes.CooldownAttribute)
                {
                    DataMethods.SendErrorLogs($"WARNING: {e.Context.User.Username}, the command is on cooldown.");
                }
                else if (firstCheck is DSharpPlus.CommandsNext.Attributes.RequireUserPermissionsAttribute)
                {
                    DataMethods.SendErrorLogs($"WARNING: {e.Context.User.Username}, you do not have the required permissions to use this command.");
                }
            }
            else
            {
                DataMethods.SendErrorLogs($"{e.Context.Guild.Name} | {e.Context.Channel} | {e.Context.User.Username} | {e.Context.Message.Content} | Error: {e.Exception}");
            }
        }
        private async Task ClientReady(DiscordClient sender, ReadyEventArgs e)
        {
            new DiscordEvents(_services).EventsFeedback(sender);
            await Task.CompletedTask;
        }
    }
}