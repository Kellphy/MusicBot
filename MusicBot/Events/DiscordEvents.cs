using DiscordBot;
using DiscordBot.Commands;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Lavalink;
using DSharpPlus.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static DiscordBot.Commands.VoiceSlashCommands;

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
                _ = Task.Run(() => GuildDownloadCompleted(client, e));
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
            client.VoiceStateUpdated += (DiscordClient client, VoiceStateUpdateEventArgs e) =>
            {
                _ = Task.Run(async () =>
                {
                    var clientMember = await e.Guild.GetMemberAsync(client.CurrentUser.Id);
                    if (clientMember?.VoiceState?.Channel != null
                    && clientMember.VoiceState.Channel.Users.Count < 2)
                    {
                        DataMethods.SendLogs($"No more users in the voice channel");
                        await new VoiceSlashCommands().VoiceActions(client, e.Guild, e.User.Id, VoiceAction.Stop, e.Before.Channel.Id, skipChecks: true);
                    }
                });
                return Task.CompletedTask;
            };
            await Task.CompletedTask;
        }


        Regex[] regexS = new Regex[]
        {
            new Regex("<a href=\"/Kellphy/MusicBot/releases/tag/(?<version>.*?)\">")
        };

        private async void GuildDownloadCompleted(DiscordClient client, GuildDownloadCompletedEventArgs e)
        {
            await Status(client, "Connecting ...");
            ConnectLavaLink(client);
            //await VersionInitialization(client, e);

        }
        private async Task Status(DiscordClient client, string newStatus = "")
        {
            DiscordActivity discordActivity;
            if (newStatus.Length > 0)
            {
                discordActivity = new DiscordActivity(newStatus, ActivityType.Playing);
            }
            else
            {
                discordActivity = new DiscordActivity(
                    $"/play{CustomStrings.space}|{CustomStrings.space}Version{CustomStrings.space}{CustomStrings.version}",
                    ActivityType.Playing);
            }
            await client.UpdateStatusAsync(discordActivity);
        }
        async void ConnectLavaLink(DiscordClient client)
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
            await Status(client);
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
                                        await member.SendMessageAsync(DataMethods.SimpleEmbed($"Upgrade available from {CustomStrings.version} to {newVersion}!", "[Download the lastest MusicBot.zip and overwrite your files!](https://github.com/Kellphy/MusicBot/releases)\nThe only files that you want to keep between updates are your **config** and **links** files"));
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
        async void InteractionCreatedMethod(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            try
            {
                List<string> to_compare = new List<string> { "voice" };
                DiscordInteractionResponseBuilder builder = new();

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
                                VoiceAction action = VoiceAction.None;
                                int toSkip = 1;
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
                                    case "skip50":
                                        action = VoiceAction.Skip;
                                        toSkip = 50;
                                        break;
                                    case "pause":
                                        action = VoiceAction.Pause;
                                        break;
                                    case "resume":
                                        action = VoiceAction.Resume;
                                        break;
                                    case "stop":
                                        action = VoiceAction.Stop;
                                        break;
                                    //case "retry":
                                    //    string searchUrl = e.Message.Embeds.FirstOrDefault().Description;
                                    //    DiscordMember member = await e.Guild.GetMemberAsync(e.User.Id);

                                    //    await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                                    //        (await new VoiceSlashCommands().Play(client, e.Guild, member, e.Channel, searchUrl)).AsEphemeral());
                                    //    return;
                                    case "queue":
                                        await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                                            new DiscordInteractionResponseBuilder().AddEmbed(new VoiceSlashCommands().Queue()).AsEphemeral());
                                        return;
                                }
                                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                                    await new VoiceSlashCommands().VoiceActions(client, e.Guild, e.User.Id, action, e.Channel.Id, skips: toSkip));

                                await Task.Delay(TimeSpan.FromSeconds(5));
                                await e.Interaction.DeleteOriginalResponseAsync();
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
    public static class DiscordEventsExtension
    {
        public static async Task<DiscordMessage> SendMessageFromInteractionBuilder(this DiscordChannel channel, DiscordInteractionResponseBuilder builder)
        {
            DiscordMessageBuilder messageBuilder = new()
            {
                Content = builder.Content,
                Embed = builder.Embeds.FirstOrDefault()
            };
            messageBuilder.AddComponents(builder.Components);

            return await channel.SendMessageAsync(messageBuilder);
        }
    }
}
