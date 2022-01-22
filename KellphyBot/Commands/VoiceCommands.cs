using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public class VoiceCommands : BaseCommandModule
    {
        DiscordMessage message;
        ulong lastKnownChannelId;
        string linksPath = "links.txt";

        public enum VoiceAction
        {
            None,
            Skip,
            Pause,
            Resume,
            Stop,
            Disconnect
        }

        public struct TrackDetails
        {
            public ulong ChannelId;
            public DiscordMember Member;
            public string Link;
            public string Title;
            public TimeSpan Length;
        }
        static LinkedList<TrackDetails> trackList = new LinkedList<TrackDetails>();

        public struct Lavalink
        {
            public LavalinkNodeConnection node;
            public LavalinkGuildConnection conn;
        }
        Lavalink lavalink = new();

        public record HardLinks
        {
            public string name;
            public string link;
        }
        private readonly List<HardLinks> hardLinks = new();

        public VoiceCommands()
        {
            AssignShortcuts();
        }

        public void AssignShortcuts()
        {
            if (File.Exists(linksPath))
            {
                string[] results = File.ReadAllLines(linksPath);

                foreach (string result in results)
                {
                    string[] nameAndLink = result.Split(' ');
                    if (nameAndLink.Length == 2)
                    {
                        hardLinks.Add(new HardLinks() { name = nameAndLink[0], link = nameAndLink[1] });
                    }
                }
            }
        }

        [Command("play")]
        public async Task Play_CC(CommandContext ctx, [RemainingText] string searchTitles)
        {
            await Play_MC(new MyContext(ctx), searchTitles);
        }
        public async Task Play_MC(MyContext ctx, string searchTitles)
        {
            if (searchTitles == null || searchTitles.Length < 1)
            {
                await Help_MC(ctx, ctx.CommandName);
                return;
            }

            await Play(ctx.Client, ctx.Guild, ctx.Channel, ctx.Member, searchTitles, ctx.Message);
        }

        public async Task<bool> InitializationAndChecks(DiscordClient client, DiscordGuild guild, DiscordChannel channel, DiscordMember member)
        {
            if (member.VoiceState == null || member.VoiceState.Channel == null)
            {
                await DataMethods.SendMessageWithLog(channel, $"{member.Mention}, you are not in a voice channel.");
                return false;
            }

            var extension = client.GetLavalink();
            if (!extension.ConnectedNodes.Any())
            {
                await DataMethods.SendMessageWithLog(channel, $"{member.Mention}, the Lavalink connection is not established");
                return false;
            }

            lavalink.node = extension.ConnectedNodes.Values.First();
            DiscordChannel channelVoice = member.VoiceState.Channel;
            if (lavalink.node.GetGuildConnection(channelVoice.Guild) == null)
            {
                RemoveTracks();
            }

            if (lavalink.node.ConnectedGuilds.Count > 1 || (lavalink.node.ConnectedGuilds.Count == 1 && lavalink.node.ConnectedGuilds.First().Value.Guild.Id != guild.Id))
            {
                await DataMethods.SendMessageWithLog(channel, $"{member.Mention}, you are trying to connect to more than 1 server.This is not currently supported. Please launch another instance of this bot.");
                return false;
            }

            await lavalink.node.ConnectAsync(channelVoice);

            lavalink.conn = lavalink.node.GetGuildConnection(guild);
            if (lavalink.conn == null)
            {
                await DataMethods.SendMessageWithLog(channel, $"{member.Mention}, Lavalink is not connected.");
                return false;
            }

            if (member.VoiceState.Channel != lavalink.conn.Channel)
            {
                await DataMethods.SendMessageWithLog(channel, $"{member.Mention}, the bot is in a different voice channel.");
                return false;
            }
            return true;
        }
        public async Task Play(DiscordClient client, DiscordGuild guild, DiscordChannel channel, DiscordMember member, string searchTitles, DiscordMessage messageToDelete = null)
        {
            var hardLink = hardLinks.Where(t => t.name == searchTitles).FirstOrDefault();
            if (hardLink != null)
            {
                searchTitles = hardLink.link;
            }

            if (!await InitializationAndChecks(client, guild, channel, member))
            {
                return;
            }

            LavalinkSearchType[] searchTypes = new LavalinkSearchType[] { LavalinkSearchType.Youtube, LavalinkSearchType.SoundCloud };

            string[] searchArr = searchTitles.Split('\n');
            int queuedSongs = 0;
            foreach (string search in searchArr)
            {
                LavalinkLoadResult loadResult = await lavalink.node.Rest.GetTracksAsync(search, LavalinkSearchType.Plain);
                bool found = false;
                foreach (LavalinkSearchType searchType in searchTypes)
                {
                    if (loadResult.LoadResultType != LavalinkLoadResultType.LoadFailed
                        && loadResult.LoadResultType != LavalinkLoadResultType.NoMatches)
                    {
                        found = true;
                        break;
                    }
                    loadResult = await lavalink.node.Rest.GetTracksAsync(search, searchType);
                }
                if (found == false)
                {
                    await DataMethods.SendMessageWithLog(channel, $"{member.Mention}, track search failed for {search}");
                    int songCount = TrackCount();
                    if (lavalink.conn.IsConnected && songCount < 1)
                    {
                        lavalink.conn.DiscordWebSocketClosed -= DiscordWebSocketClosed;
                        lavalink.conn.PlaybackStarted -= PlaybackStarted;
                        lavalink.conn.PlaybackFinished -= PlaybackFinished;
                        await lavalink.conn.DisconnectAsync();
                    }
                    return;
                }

                IEnumerable<LavalinkTrack> trackList = new List<LavalinkTrack>() { loadResult.Tracks.First() };

                if (search.Substring(0, 4) == "http")
                {
                    trackList = loadResult.Tracks;
                }
                List<string> tracksToQueue = new List<string>();
                List<string> trackTitles = new List<string>();
                List<TimeSpan> trackLengths = new List<TimeSpan>();
                foreach (LavalinkTrack track in trackList)
                {
                    int songCount = TrackCount();
                    if (songCount < 1)
                    {
                        lavalink.conn.PlaybackFinished += PlaybackFinished;
                        lavalink.conn.PlaybackStarted += PlaybackStarted;
                        lavalink.conn.DiscordWebSocketClosed += DiscordWebSocketClosed;
                        await lavalink.conn.PlayAsync(track);

                        //For the first song
                        AddTracks(channel.Id, member, new List<string>() { track.Uri.ToString() }, new List<string>() { track.Title }, new List<TimeSpan>() { track.Length });

                        await SendEmbedWithButtoms(channel, SongEmbed(track, "Playing", member.Username));
                    }
                    else
                    {
                        if (searchArr.Length < 2 && trackList.Count() < 2)
                        {
                            DiscordMessage queueMessageToDelete = await DataMethods.SendMessageWithLog(channel, SongEmbed(track, $"Queued", member.Username), $"[Queued] {track.Title}");
                            DataMethods.DeleteDiscordMessage(queueMessageToDelete, TimeSpan.FromSeconds(5));
                        }
                        else
                        {
                            queuedSongs++;
                        }
                        tracksToQueue.Add(track.Uri.ToString());
                        trackTitles.Add(track.Title);
                        trackLengths.Add(track.Length);
                    }
                }

                //For the queue
                AddTracks(channel.Id, member, tracksToQueue, trackTitles, trackLengths);
            }
            if (queuedSongs > 0)
            {
                string songOrSongs = queuedSongs == 1 ? "song" : "songs";
                DiscordMessage queueMessageToDelte = await DataMethods.SendMessageWithLog(channel, DataMethods.SimpleEmbed("Queued", $"{queuedSongs} {songOrSongs}"), $"[Queued] {queuedSongs} {songOrSongs}");
                DataMethods.DeleteDiscordMessage(queueMessageToDelte, TimeSpan.FromSeconds(5));
            }
            if (messageToDelete != null)
            {
                await messageToDelete.DeleteAsync();
            }
        }

        [Command("skip")]
        public async Task Skip_CC(CommandContext ctx)
        {
            await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Skip, ctx.Channel.Id, ctx.Message);
        }
        [Command("skip")]
        public async Task Skip_CC(CommandContext ctx, int skips)
        {
            await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Skip, ctx.Channel.Id, ctx.Message, skips);
        }
        [Command("pause")]
        public async Task Pause_CC(CommandContext ctx)
        {
            await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Pause, ctx.Channel.Id, ctx.Message);
        }
        [Command("resume")]
        public async Task Resume_CC(CommandContext ctx)
        {
            await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Resume, ctx.Channel.Id, ctx.Message);
        }
        [Command("stop")]
        public async Task Stop_CC(CommandContext ctx)
        {
            await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Stop, ctx.Channel.Id, ctx.Message);
        }
        public async Task VoiceActions(DiscordClient client, DiscordGuild guild, ulong userId, VoiceAction action, ulong channelId, DiscordMessage messageToDelete = null, int skips = 1)
        {
            DiscordChannel channel;
            if (channelId == 0)
            {
                channel = guild.GetChannel(trackList.Last.Value.ChannelId);
            }
            else
            {
                channel = guild.GetChannel(channelId);
            }

            DiscordMember member = await guild.GetMemberAsync(userId);
            if (!await InitializationAndChecks(client, guild, channel, member))
            {
                return;
            }

            string actionString = string.Empty;
            string skipString = skips > 1 ? $" {skips}" : string.Empty;
            switch (action)
            {
                case VoiceAction.Pause:
                    actionString = "Paused";
                    break;
                case VoiceAction.Resume:
                    actionString = "Resumed";
                    break;
                case VoiceAction.Skip:
                    actionString = "Skipped";
                    break;
                case VoiceAction.Stop:
                    actionString = "Stopped";
                    break;
                case VoiceAction.Disconnect:
                    await VoiceDisconnect(lavalink.conn, "Everyone Left.");
                    return;
            }

            DiscordMessage actionMessageToDelete = await DataMethods.SendMessageWithLog(channel, DataMethods.SimpleEmbed($"{actionString}{skipString} by {member.Username}"), $"[{actionString}{skipString}] by {member.Username}");
            DataMethods.DeleteDiscordMessage(actionMessageToDelete, TimeSpan.FromSeconds(5));

            switch (action)
            {
                case VoiceAction.Pause:
                    await lavalink.conn.PauseAsync();
                    break;
                case VoiceAction.Resume:
                    await lavalink.conn.ResumeAsync();
                    break;
                case VoiceAction.Skip:
                    RemoveTracks(skips - 1);
                    await lavalink.conn.StopAsync();
                    break;
                case VoiceAction.Stop:
                    await EditLastMessage();
                    await VoiceDisconnect(lavalink.conn);
                    break;
            }
            if (messageToDelete != null)
            {
                await messageToDelete.DeleteAsync();
            }
        }

        [Command("queue")]
        public async Task Queue_CC(CommandContext ctx)
        {
            await Queue(ctx.Channel, ctx.Message);
        }
        public async Task Queue(DiscordChannel channel, DiscordMessage messageToDelete = null)
        {
            int maxQueueInt = 10;
            string queueList = string.Empty;
            int maxQueue = Math.Min(TrackCount(), maxQueueInt);
            for (int i = 0; i < maxQueue; i++)
            {
                string prefix = i == 0 ? "Playing" : i.ToString();
                string length = trackList.ElementAt(i).Length == TimeSpan.Zero ? "LIVE" : trackList.ElementAt(i).Length.ToString();
                queueList += $"```\n[{prefix}] - [{length}] {trackList.ElementAt(i).Title}\n```";
            }
            if (TrackCount() > maxQueueInt) queueList += $"**+ {TrackCount() - maxQueueInt} more**";
            if (queueList.Length < 1)
            {
                queueList = "No songs in queue.";
            }
            DiscordMessage queueMessageToDelte = await DataMethods.SendMessageWithLog(channel, DataMethods.SimpleEmbed("Queue", queueList), $"[Queue]: {TrackCount()}");
            DataMethods.DeleteDiscordMessage(queueMessageToDelte, TimeSpan.FromSeconds(30));
            if (messageToDelete != null)
            {
                await messageToDelete.DeleteAsync();
            }
        }

        public async Task Help_MC(MyContext ctx, string commandDef)
        {
            await Help(ctx.Client, ctx.Channel, ctx.CommandName, commandDef);
        }
        public async Task Help(DiscordClient client, DiscordChannel channel, string commandName, string commandDef)
        {
            string command = commandDef.ToLower();

            var builder = new DiscordMessageBuilder();

            var helpEmbed = new DiscordEmbedBuilder
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = client.CurrentUser.AvatarUrl,
                    Name = $"Kompanion | {command}",
                    Url = "https://kellphy.com/discord"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Command: {commandName}",
                },
                Color = DiscordColor.CornflowerBlue
            };

            switch (command)
            {
                //case "play":
                //case "stop":
                //case "pause":
                //case "skip":
                //case "queue":
                //case "music":
                //case "resume":
                default:
                    helpEmbed.AddField("Details",
                        "⮚ You can add multiple songs with the same `play` command by using a line break with the key combination \"__Shift+Enter__\".");
                    string localLinks = hardLinks.Any() ? string.Join(", ", hardLinks.Select(t => t.name)) : "Check out [this](https://kellphy.com/musicbot) guide to add link shortcuts.";
                    helpEmbed.AddField("Your Shortcuts",
                        $"⮚ {localLinks}");
                    helpEmbed.AddField("Commands",
                        "```md" +
                        "\nplay" +
                        "\n# Shows help." +
                        "\n``````md" +
                        "\nplay [Name|Link|Playlist]" +
                        "\n# Play / Queue a new song or playlist." +
                        "\n``````md" +
                        "\nskip" +
                        "\n# Skip to the next song." +
                        "\n``````md" +
                        "\nskip [NumberOfSongs]" +
                        "\n# Skip a certain number of songs." +
                        "\n``````md" +
                        "\nqueue" +
                        "\n# Display the song queue." +
                        "\n``````md" +
                        "\npause" +
                        "\n``````md" +
                        "\nresume" +
                        "\n``````md" +
                        "\nstop" +
                        "\n```");
                    break;
            }
            helpEmbed.AddField(CustomStrings.embedBreak, CustomStrings.embedLinks);

            builder.WithEmbed(helpEmbed);
            await channel.SendMessageAsync(builder);
        }

        private async Task DiscordWebSocketClosed(LavalinkGuildConnection sender, DSharpPlus.Lavalink.EventArgs.WebSocketCloseEventArgs e)
        {
            sender.DiscordWebSocketClosed -= DiscordWebSocketClosed;
            sender.PlaybackStarted -= PlaybackStarted;
            sender.PlaybackFinished -= PlaybackFinished;
            await Task.CompletedTask;
        }
        private async Task PlaybackFinished(LavalinkGuildConnection sender, DSharpPlus.Lavalink.EventArgs.TrackFinishEventArgs e)
        {
            await ContinueOrEnd(sender);
        }
        async Task ContinueOrEnd(LavalinkGuildConnection sender)
        {
            await EditLastMessage();

            if (sender.IsConnected)
            {
                if (TrackCount() > 1)
                {
                    RemoveTracks(1);
                    var queuedTrack = GetTrack();
                    DiscordChannel channel = sender.Guild.GetChannel(queuedTrack.ChannelId);
                    LavalinkLoadResult loadResult = await sender.Node.Rest.GetTracksAsync(queuedTrack.Link, LavalinkSearchType.Plain);
                    if (loadResult.LoadResultType != LavalinkLoadResultType.LoadFailed && loadResult.LoadResultType != LavalinkLoadResultType.NoMatches)
                    {
                        await sender.PlayAsync(loadResult.Tracks.FirstOrDefault());
                        await SendEmbedWithButtoms(channel, SongEmbed(loadResult.Tracks.FirstOrDefault(), "Playing", queuedTrack.Member.Username));
                    }
                    else
                    {
                        await DataMethods.SendMessageWithLog(channel, $"{queuedTrack.Member.Mention}, track search failed for {queuedTrack.Link}");
                        await ContinueOrEnd(sender);
                    }
                }
                else
                {
                    await VoiceDisconnect(sender, "Queue Finished.");
                }
            }
        }
        async Task VoiceDisconnect(LavalinkGuildConnection sender, string reason = "")
        {
            if (reason.Length > 1 && lastKnownChannelId != 0)
            {
                DiscordChannel channel = sender.Guild.GetChannel(lastKnownChannelId);
                DiscordMessage messageToDelete = await DataMethods.SendMessageWithLog(channel, DataMethods.SimpleEmbed($"{reason} Disconnected."), $"{reason} Disconnected.");
                DataMethods.DeleteDiscordMessage(messageToDelete, TimeSpan.FromSeconds(5));
            }
            RemoveTracks();
            sender.DiscordWebSocketClosed -= DiscordWebSocketClosed;
            sender.PlaybackStarted -= PlaybackStarted;
            sender.PlaybackFinished -= PlaybackFinished;
            if (sender.IsConnected)
                await sender.DisconnectAsync();
        }
        private Task PlaybackStarted(LavalinkGuildConnection sender, DSharpPlus.Lavalink.EventArgs.TrackStartEventArgs e)
        {
            return Task.CompletedTask;
        }

        DiscordEmbed SongEmbed(LavalinkTrack track, string playing, string user)
        {
            string playingTimer = string.Empty;
            if (playing == "Playing" && track.Length != TimeSpan.Zero)
            {
                playingTimer = "\nEnds " + DataMethods.UnixUntil(DateTime.Now + track.Length);
            }
            string length = track.Length == TimeSpan.Zero ? "LIVE" : track.Length.ToString();
            return new DiscordEmbedBuilder
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"[{length}] " + track.Title + $" ({track.Author})"
                },
                Title = $"{playingTimer}",
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Queued by {user}"
                },
                Description = $"{track.Uri}",
                Color = playing == "Playing" ? DiscordColor.DarkGreen : DiscordColor.Orange
            };
        }
        private async Task<DiscordMessage> SendEmbedWithButtoms(DiscordChannel channel, DiscordEmbed embed)
        {
            DiscordMessageBuilder builder = new DiscordMessageBuilder();
            builder.WithEmbed(embed);
            builder.AddComponents(new DiscordComponent[]
                {
                        new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_retry", "Requeue This Song"),
                        new DiscordButtonComponent(ButtonStyle.Primary, "kb_voice_pause", "Pause"),
                        new DiscordButtonComponent(ButtonStyle.Success, "kb_voice_resume", "Resume"),
                        new DiscordButtonComponent(ButtonStyle.Danger, "kb_voice_stop", "Stop"),
                });
            builder.AddComponents(new DiscordComponent[]
                {
                        new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_queue", "Show Queue"),
                        new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_skip", "Skip"),
                        new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_skip5", "Skip 5"),
                        new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_skip10", "Skip 10"),
                });
            message = await DataMethods.SendMessageWithLog(channel, builder, $"[Playing] {embed.Author.Name}");
            return message;
        }
        private async Task EditLastMessage()
        {
            if (message != null)
            {
                try
                {
                    DiscordMessageBuilder builder = new DiscordMessageBuilder();
                    DiscordEmbed embed = message.Embeds.FirstOrDefault();

                    DiscordEmbedBuilder newEmbed = new DiscordEmbedBuilder
                    {
                        Author = new DiscordEmbedBuilder.EmbedAuthor
                        {
                            Name = embed.Author.Name
                        },
                        Description = embed.Description,
                        Color = DiscordColor.VeryDarkGray
                    };

                    builder.WithEmbed(newEmbed);
                    builder.AddComponents(new DiscordComponent[]
                        {
                        new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_retry", "Requeue This Song"),
                        });

                    await message.ModifyAsync(builder);
                }
                catch
                {
                    DataMethods.SendErrorLogs("Could not update last message.");
                }
            }
        }
        private void RemoveTracks(int? skips = null)
        {
            if (skips is null || skips >= TrackCount())
            {
                trackList.Clear();
            }
            else
            {
                for (int i = 0; i < skips; i++)
                {
                    trackList.RemoveFirst();
                }
            }
        }
        private int TrackCount()
        {
            return trackList.Count;
        }
        private TrackDetails GetTrack()
        {
            var track = trackList.First();
            lastKnownChannelId = track.ChannelId;
            return track;
        }
        private void AddTracks(ulong channelId, DiscordMember member, List<string> tracks, List<string> titles, List<TimeSpan> lengths)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                trackList.AddLast(new TrackDetails() { ChannelId = channelId, Member = member, Link = tracks[i], Title = titles[i], Length = lengths[i] });
            }
        }
    }
}