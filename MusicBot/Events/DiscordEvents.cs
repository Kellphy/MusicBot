using DiscordBot;
using DiscordBot.Commands;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Lavalink;
using DSharpPlus.Net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static DiscordBot.Commands.VoiceCommands;

namespace MusicBot.Events
{
    public class DiscordEvents
    {
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
        public async void EventsFeedback(DiscordClient client, bool ignorePrefix, string prefix)
        {
            if (ignorePrefix)
            {
                client.MessageCreated += (DiscordClient client, MessageCreateEventArgs e) =>
                {
                    _ = Task.Run(() => MessageCreatedMethod(client, e));
                    return Task.CompletedTask;
                };
            }
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
                _ = Task.Run(() => GuildDownloadCompleted(client, e, prefix));
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

        private async void GuildDownloadCompleted(DiscordClient client, GuildDownloadCompletedEventArgs e, string prefix)
        {
            await Status(client, prefix, "Connecting ...");
            ConnectLavaLink(client, prefix);
            await VersionInitialization(client, e);

        }
        private async Task Status(DiscordClient client, string prefix, string newStatus = "")
        {
            DiscordActivity discordActivity;
            if (newStatus.Length > 0)
            {
                discordActivity = new DiscordActivity(newStatus, ActivityType.Playing);
            }
            else
            {
                discordActivity = new DiscordActivity(
                    $"{prefix}play{CustomStrings.space}|{CustomStrings.space}Version{CustomStrings.space}{CustomStrings.version} " +
                    $"Music{CustomStrings.space}bot:{CustomStrings.space}kellphy.com/musicbot " +
                    $"Full{CustomStrings.space}bot:{CustomStrings.space}kellphy.com/kompanion ",
                    ActivityType.Playing);
            }
            await client.UpdateStatusAsync(discordActivity);
        }
        async void ConnectLavaLink(DiscordClient client, string prefix)
        {
            string endpointHost;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                endpointHost = "127.0.0.1";
            }
            else
            {
                endpointHost = "lvl";
            }

            var endpoint = new ConnectionEndpoint
            {
                Hostname = endpointHost, // From your server configuration.
                //Hostname = "127.0.0.1", // From your server configuration.
                Port = 2333 // From your server configuration
            };

            var lavalinkConfig = new LavalinkConfiguration
            {
                Password = "youshallnotpass", // From your server configuration.
                RestEndpoint = endpoint,
                SocketEndpoint = endpoint
            };

            var lavalink = client.UseLavalink();
            await lavalink.ConnectAsync(lavalinkConfig); // Make sure this is after Discord.ConnectAsync(). 

            DataMethods.SendLogs("Lavalink Connected!");
            await Status(client, prefix);
        }
        async Task VersionInitialization(DiscordClient client, GuildDownloadCompletedEventArgs e)
        {
            try
            {
                using (Stream stream = await new HttpClient().GetStreamAsync("https://github.com/Kellphy/MusicBot/tags"))
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
                                        await member.SendMessageAsync(DataMethods.SimpleEmbed($"Upgrade available from {CustomStrings.version} to {newVersion}!", "[Download the lastest MusicBot.zip and overwrite your files!](https://github.com/Kellphy/MusicBot/releases)\nThe only files that you want to keep between updates are your **config** and **links** files."));
                                        DataMethods.SendErrorLogs($"Upgrade available from {CustomStrings.version} to {newVersion}: https://github.com/Kellphy/MusicBot/releases");
                                    }
                                }
                            }
                        }
                    }
                }
                DataMethods.SendLogs($"Version Check Completed!");
            }
            catch (Exception ex)
            {
                DataMethods.SendErrorLogs($"Version Initialization Incomplete: {ex}");
            }
            await Task.CompletedTask;
        }
        async void MessageCreatedMethod(DiscordClient client, MessageCreateEventArgs e)
        {
            switch (e.Message.Content.ToLowerInvariant())
            {
                case string s when s.StartsWith("play"):
                    string searchTitles = e.Message.Content.Replace("play", "").Trim();
                    if (searchTitles == null || searchTitles.Length < 1)
                    {
                        await new VoiceCommands().Help(client, e.Channel, "play", "play");
                        return;
                    }
                    DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
                    await new VoiceCommands().Play(client, e.Guild, e.Channel, member, searchTitles, e.Message);
                    break;
                case string s when s.StartsWith("skip"):

                    string skipsString = e.Message.Content.Replace("skip", "").Trim();
                    if (skipsString == null || skipsString.Length < 1)
                    {
                        await new VoiceCommands().VoiceActions(client, e.Guild, e.Author.Id, VoiceAction.Skip, e.Channel.Id, e.Message);
                    }
                    if (Int32.TryParse(skipsString, out int skips))
                    {
                        await new VoiceCommands().VoiceActions(client, e.Guild, e.Author.Id, VoiceAction.Skip, e.Channel.Id, e.Message, skips);
                    }
                    break;
                case string s when s.StartsWith("pause"):
                    await new VoiceCommands().VoiceActions(client, e.Guild, e.Author.Id, VoiceAction.Pause, e.Channel.Id, e.Message);
                    break;
                case string s when s.StartsWith("resume"):
                    await new VoiceCommands().VoiceActions(client, e.Guild, e.Author.Id, VoiceAction.Resume, e.Channel.Id, e.Message);
                    break;
                case string s when s.StartsWith("stop"):
                    await new VoiceCommands().VoiceActions(client, e.Guild, e.Author.Id, VoiceAction.Stop, e.Channel.Id, e.Message, owner: true);
                    break;
                case string s when s.StartsWith("queue"):
                    await new VoiceCommands().Queue(e.Channel, e.Message);
                    break;
            }
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
                                VoiceAction action = VoiceAction.None;
                                int toSkip = 1;
                                bool owner = false;
                                switch (id)
                                {
                                    case "skip":
                                        action = VoiceAction.Skip;
                                        break;
                                    case "skip5":
                                        action = VoiceAction.Skip;
                                        toSkip = 5;
                                        break;
                                    case "skip10":
                                        action = VoiceAction.Skip;
                                        toSkip = 10;
                                        break;
                                    case "pause":
                                        action = VoiceAction.Pause;
                                        break;
                                    case "resume":
                                        action = VoiceAction.Resume;
                                        break;
                                    case "stop":
                                        action = VoiceAction.Stop;
                                        owner = true;
                                        break;
                                    case "retry":
                                        string searchUrl = e.Message.Embeds.FirstOrDefault().Description;
                                        DiscordMember member = await e.Guild.GetMemberAsync(e.User.Id);
                                        await new VoiceCommands().Play(client, e.Guild, e.Channel, member, searchUrl);
                                        return;
                                    case "queue":
                                        await new VoiceCommands().Queue(e.Channel);
                                        return;
                                }
                                await new VoiceCommands().VoiceActions(client, e.Guild, e.User.Id, action, e.Channel.Id, skips: toSkip, owner: owner);
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
