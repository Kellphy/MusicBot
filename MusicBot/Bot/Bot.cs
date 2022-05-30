using DiscordBot.Commands;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Lavalink;
using DSharpPlus.Net;
using MusicBot.Events;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    public struct ConfigJson
    {
        [JsonProperty("token")]
        public string Token { get; private set; }
        [JsonProperty("prefix")]
        public string Prefix { get; private set; }
    }
    public class Bot
    {
        public DiscordClient Client { get; private set; }
        public CommandsNextExtension Commands { get; private set; }

        bool ignorePrefix;

        private readonly IServiceProvider _services;

        ConfigJson configJson;
        public Bot(IServiceProvider services)
        {
            Console.Clear();
            DataMethods.SendKellphy();
            DataMethods.SendLogs($"Version: {CustomStrings.version}");

            _services = services;
            //Config.json
            var json = string.Empty;

            if (!File.Exists("config.json"))
            {
                DataMethods.SendErrorLogs("config.json is missing from your directory.");
                return;
            }

            using (var fs = File.OpenRead("config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = sr.ReadToEnd();
            configJson = JsonConvert.DeserializeObject<ConfigJson>(json);

            if (configJson.Token.Length < 30)
            {
                DataMethods.SendErrorLogs("I'm pretty sure you forgot to add your token. Be sure to not override it when updating.");
            }
            if (configJson.Prefix.Length > 3)
            {
                DataMethods.SendErrorLogs($"Your prefix \"{configJson.Prefix}\" is so long that I decided to ignore it.");
                ignorePrefix = true;
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
            Commands.RegisterCommands<AdminCommands>();

            //var slashConfig = new SlashCommandsConfiguration
            //{
            //    Services = services
            //};
            //var slash = Client.UseSlashCommands(slashConfig);

            //slash.RegisterCommands<SlashCommands>();

            Commands.CommandErrored += OnCommandError;
            Commands.CommandExecuted += OncommandExecute;
            //slash.SlashCommandErrored += OnSlashCommandError;
            //slash.SlashCommandExecuted += OnSlashCommandExecute;
            //Connect bot
            Client.ConnectAsync();
        }


        //private async Task OnSlashCommandExecute(SlashCommandsExtension sender, SlashCommandExecutedEventArgs e)
        //{
        //    DataMethods.SendLogs(new MyContext(e.Context));
        //    await Task.CompletedTask;
        //}
        //private async Task OnSlashCommandError(SlashCommandsExtension sender, SlashCommandErrorEventArgs e)
        //{
        //    VoiceCommands voice = new VoiceCommands();
        //    if (e.Exception is ArgumentException)
        //    {
        //        await voice.Help(new MyContext(e.Context), e.Context.CommandName);
        //    }
        //    else if (e.Exception is ChecksFailedException)
        //    {
        //        DataMethods.SendErrorLogs($"WARNING: {e.Context.User.Username}, you do not have the required permissions or you keep spamming the command.");
        //    }
        //    else
        //    {
        //        DataMethods.SendErrorLogs($"{e.Context.Guild.Name} | {e.Context.Channel} | {e.Context.User.Username} | Error: {e.Exception}");
        //    }
        //}

        private async Task OncommandExecute(CommandsNextExtension sender, CommandExecutionEventArgs e)
        {
            DataMethods.SendLogs(new MyContext(e.Context));
            await Task.CompletedTask;
        }
        private async Task OnCommandError(CommandsNextExtension sender, CommandErrorEventArgs e)
        {
            if (e.Exception is ArgumentException)
            {
                VoiceCommands voice = new VoiceCommands();
                await voice.Help_MC(new MyContext(e.Context), e.Command.Name);
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
            new DiscordEvents().EventsFeedback(sender, ignorePrefix, ignorePrefix ? "" : configJson.Prefix);
            await Task.CompletedTask;
        }
    }
}