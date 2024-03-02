using DiscordBot.Commands;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using MusicBot.Events;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace DiscordBot
{
	public struct ConfigJson
    {
        [JsonProperty("token")]
        public string Token { get; private set; }
        [JsonProperty("prefix")]
        public string Prefix { get; private set; }
		[JsonProperty("isOwnerStarted")]
		public bool IsOwnerStarted { get; private set; }
	}
    public class Bot
    {
        public DiscordClient Client { get; private set; }
        public CommandsNextExtension Commands { get; private set; }

        private readonly IServiceProvider _services;

        public Bot(IServiceProvider services)
        {
            Console.Clear();
            DataMethods.SendKellphy();
            DataMethods.SendLogs($"Version: {CustomAttributes.version}");

            _services = services;
            //Config.json
            var json = string.Empty;

            if (!File.Exists("config.json"))
            {
                DataMethods.SendErrorLogs("config.json is missing from your directory");
                return;
            }

            using (var fs = File.OpenRead("config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = sr.ReadToEnd();
			CustomAttributes.configJson = JsonConvert.DeserializeObject<ConfigJson>(json);

			if (CustomAttributes.configJson.Token.Length < 30)
            {
                DataMethods.SendErrorLogs("I'm pretty sure you forgot to add your token. Be sure to not override it when updating");
            }

            var config = new DiscordConfiguration
            {
                Token = CustomAttributes.configJson.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Error,
                Intents = DiscordIntents.AllUnprivileged,
            };

            Client = new DiscordClient(config);

            Client.UseInteractivity(new InteractivityConfiguration
            {
                //How much to wait for a command
                Timeout = TimeSpan.FromMinutes(5)
            });

            //Setup commands
            var commandsConfig = new CommandsNextConfiguration
            {
                StringPrefixes = new string[] { CustomAttributes.configJson.Prefix },
                EnableDms = false,
                EnableMentionPrefix = true,
                DmHelp = false,
                EnableDefaultHelp = false,
                Services = services,
            };
            var slashConfig = new SlashCommandsConfiguration
            {
                Services = _services
            };
            Commands = Client.UseCommandsNext(commandsConfig);
            Commands.RegisterCommands<AdminCommands>();
            var slashCommands = Client.UseSlashCommands(slashConfig);
            slashCommands.RegisterCommands<VoiceSlashCommands>();

            //slash.SlashCommandErrored += OnSlashCommandError;
            //slash.SlashCommandExecuted += OnSlashCommandExecute;
            //Connect bot
            Client.ConnectAsync();

			new DiscordEvents().EventsFeedback(Client);
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
    }
}