using DiscordBot;
using DiscordBot.Commands;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KellphyBot.Events
{
    public class DiscordEvents
    {
        private readonly IServiceProvider _services;
        public DiscordEvents(IServiceProvider services)
        {
            _services = services;
        }

        public async Task SendEmbedWithLinks(DiscordEmbedBuilder embedLog, DiscordChannel ch)
        {
            embedLog.AddField(CustomStrings.embedBreak, CustomStrings.embedLinks);
            await ch.SendMessageAsync(embedLog);
        }
        public DiscordEmbedBuilder EventEmbed(string subject)
        {
            var endEmbed = new DiscordEmbedBuilder
            {
                Title = subject,
                Color = DiscordColor.DarkBlue
            };
            return endEmbed;
        }
        public async void EventsFeedback(DiscordClient client)
        {
            client.ClientErrored += (DiscordClient client, ClientErrorEventArgs e) =>
            {
                DataMethods.SendErrorLogs($"Client Error: {e.EventName} {e.Exception}");
                return Task.CompletedTask;
            };
            client.SocketOpened += (DiscordClient client, SocketEventArgs e) =>
            {
                DataMethods.SendLogs($"WebSocket Open");
                return Task.CompletedTask;
            };
            client.Resumed += (DiscordClient client, ReadyEventArgs e) =>
            {
                DataMethods.SendLogs($"Resumed");
                return Task.CompletedTask;
            };
            client.SocketClosed += (DiscordClient client, SocketCloseEventArgs e) =>
            {
                DataMethods.SendLogs($"WebSocket Closed: {e.CloseCode} {e.CloseMessage}");
                return Task.CompletedTask;
            };
            client.GuildUnavailable += (DiscordClient client, GuildDeleteEventArgs e) =>
            {
                DataMethods.SendErrorLogs($"Guild Unavailable: {e.Guild.Name} ({e.Guild.Id})");
                return Task.CompletedTask;
            };
            client.GuildDownloadCompleted += (DiscordClient client, GuildDownloadCompletedEventArgs e) =>
            {
                DataMethods.SendLogs($"{e.GetType().Name}");
                _ = Task.Run(() => VersionInitialization(client, e));
                return Task.CompletedTask;
            };
            client.ComponentInteractionCreated += (DiscordClient client, ComponentInteractionCreateEventArgs e) =>
            {
                Console.Write($"\u001b[36m{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} - ");
                Console.Write($"\u001b[36m{e.User.Username}#{e.User.Discriminator} ({e.User.Id}) ");
                Console.Write($"\u001b[32m{e.Guild.Name} ({e.Guild.Id}) ");
                Console.Write($"\u001b[35m{e.Channel.Name} ({e.Channel.Id}) ");
                Console.Write($"\u001b[0m\n");
                _ = Task.Run(() => InteractionCreatedMethod(client, e));
                return Task.CompletedTask;
            };
            await Task.CompletedTask;
        }

        Regex[] regexS = new Regex[]
        {
            new Regex("<a href=\"/Kellphy/MusicBot/releases/tag/(?<version>.*?)\">")
        };
        async void VersionInitialization(DiscordClient client, GuildDownloadCompletedEventArgs e)
        {
            try
            {
                using (Stream stream = WebRequest.Create($"https://github.com/Kellphy/MusicBot/tags").GetResponse().GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string html = reader.ReadToEnd();
                        foreach (Regex regex in regexS)
                        {
                            MatchCollection matches = regex.Matches(html);
                            if (matches.Count > 0 && matches.First().Success)
                            {
                                var newVersion = matches.First().Groups["version"].ToString();
                                if (newVersion != CustomStrings.version)
                                {
                                    foreach (var owner in client.CurrentApplication.Owners)
                                    {
                                        var member = await e.Guilds.First().Value.GetMemberAsync(owner.Id);
                                        await member.SendMessageAsync(DataMethods.SimpleEmbed($"Upgrade available from {CustomStrings.version} to {newVersion}!", "[Download the lastest MusicBot.zip and overwrite your files!](https://github.com/Kellphy/MusicBot/releases)\nThe only thing that you want to keep between updates is your **config** file."));
                                        DataMethods.SendErrorLogs($"Upgrade available from {CustomStrings.version} to {newVersion}: https://github.com/Kellphy/MusicBot/releases");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DataMethods.SendErrorLogs($"Version Initialization Incomplete: {ex}");
            }
            await Task.CompletedTask;
        }
        async void InteractionCreatedMethod(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            try
            {
                List<string> to_compare = new List<string> { "voice" };

                for (int i = 0; i < to_compare.Count; i++)
                {
                    int prefix = 3;

                    string comparer = e.Id.Substring(prefix, to_compare[i].Length);//Button
                    if (e.Values.Length > 0) comparer = e.Values.FirstOrDefault().Substring(prefix, to_compare[i].Length);//Select Menu

                    if (comparer == to_compare[i])
                    {
                        string id = string.Empty;
                        if (e.Values.Length > 0) id = e.Values.FirstOrDefault().Substring(prefix + to_compare[i].Length + 1);//Select Menu
                        else id = e.Id.Substring(prefix + to_compare[i].Length + 1);//Button

                        switch (i)
                        {
                            case 0://voice
                                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                                VoiceCommands.VoiceAction action = VoiceCommands.VoiceAction.None;
                                switch (id)
                                {
                                    case "skip":
                                        action = VoiceCommands.VoiceAction.Skip;
                                        break;
                                    case "pause":
                                        action = VoiceCommands.VoiceAction.Pause;
                                        break;
                                    case "resume":
                                        action = VoiceCommands.VoiceAction.Resume;
                                        break;
                                    case "stop":
                                        action = VoiceCommands.VoiceAction.Stop;
                                        break;
                                    case "retry":
                                        string searchUrl = e.Message.Embeds.FirstOrDefault().Description;
                                        DiscordMember member = await e.Guild.GetMemberAsync(e.User.Id);
                                        await new VoiceCommands(_services).Play(client, e.Guild, e.Channel, member, searchUrl);
                                        return;
                                    case "queue":
                                        await new VoiceCommands(_services).Queue(e.Channel);
                                        return;
                                }
                                await new VoiceCommands(_services).VoiceActions(client, e.Guild, e.User.Id, action, e.Channel.Id);
                                break;
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                DataMethods.SendErrorLogs($"Interaction Error: {ex}");
            }
        }
    }
}
