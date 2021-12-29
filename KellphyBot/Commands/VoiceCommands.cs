using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public class VoiceCommands : BaseCommandModule
    {
        private readonly IServiceProvider _services;
        public VoiceCommands(IServiceProvider services)
        {
            _services = services;
        }

        static LinkedList<TrackDetails> trackList = new LinkedList<TrackDetails>();

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

        public struct HardLinks
        {
            public string name;
            public bool isPublic;
            public string link;
        }

        public HardLinks[] links = new HardLinks[]
        {
            new HardLinks{name = "playlist", link = "https://kellphy.com/playlist" },
            new HardLinks{name = "test", link = "https://www.youtube.com/watch?v=qZC5gtOw3DU" },

            new HardLinks{name = "arcade", link = "https://www.youtube.com/watch?v=7tNtU5XFwrU",isPublic=true },
            new HardLinks{name = "funk", link = "https://www.youtube.com/watch?v=FxDs1r4hjOE",isPublic=true },
            new HardLinks{name = "lofi", link = "https://www.youtube.com/watch?v=5qap5aO4i9A",isPublic=true },
            new HardLinks{name = "nightcore", link = "https://www.youtube.com/watch?v=THN8ihZ6qy0",isPublic=true },
            new HardLinks{name = "spinnin", link = "https://www.youtube.com/watch?v=N65Jb683pXQ",isPublic=true },
            new HardLinks{name = "trap", link = "https://www.youtube.com/watch?v=ye7E8lFD-EA",isPublic=true },

            new HardLinks{name = "monstercat", link = "https://www.twitch.tv/monstercat" },

            new HardLinks{name = "evergreen", link = "https://emg.streamguys1.com/evergreen-website" },
            new HardLinks{name = "xmas", link = "https://emg.streamguys1.com/evergreen-website",isPublic=true },
            new HardLinks{name = "christmas", link = "https://emg.streamguys1.com/evergreen-website" },
        };

        [Command("play")]
        public async Task LavaPlay(CommandContext ctx, [RemainingText] string searchTitles)
        {
            foreach (HardLinks link in links)
            {
                if (searchTitles == link.name)
                {
                    searchTitles = link.link;
                }
            }

            if (searchTitles == null || searchTitles.Length < 1)
            {
                await Help(new MyContext(ctx), ctx.Command.Name);
                return;
            }
            await Play(ctx.Client, ctx.Guild, ctx.Channel, ctx.Member, searchTitles);
            await ctx.Message.DeleteAsync();
        }
        public async Task Play(DiscordClient client, DiscordGuild guild, DiscordChannel channel, DiscordMember member, string searchTitles)
        {
            if (member.VoiceState == null || member.VoiceState.Channel == null)
            {
                await channel.SendMessageAsync($"{member.Mention}, you are not in a voice channel.");
                return;
            }

            DiscordChannel channelVoice = member.VoiceState.Channel;
            var lava = client.GetLavalink();
            if (!lava.ConnectedNodes.Any())
            {
                await channel.SendMessageAsync($"{member.Mention}, the Lavalink connection is not established");
                return;
            }

            var node = lava.ConnectedNodes.Values.First();

            if (node.GetGuildConnection(channelVoice.Guild) == null)
            {
                RemoveTracks();
            }

            if (node.ConnectedGuilds.Count > 1 || (node.ConnectedGuilds.Count == 1 && node.ConnectedGuilds.First().Value.Guild.Id != guild.Id))
            {
                await channel.SendMessageAsync($"{member.Mention}, you are trying to connect to more than 1 server.This is not currently supported. Please launch another instance of this bot.");
                return;
            }

            await node.ConnectAsync(channelVoice);

            var conn = node.GetGuildConnection(guild);

            if (conn == null)
            {
                await channel.SendMessageAsync($"{member.Mention}, Lavalink is not connected.");
                return;
            }

            if (member.VoiceState.Channel != conn.Channel)
            {
                await channel.SendMessageAsync($"{member.Mention}, the bot is in a different voice channel.");
                return;
            }

            LavalinkSearchType[] searchTypes = new LavalinkSearchType[] { LavalinkSearchType.Youtube, LavalinkSearchType.SoundCloud };

            string[] searchArr = searchTitles.Split('\n');
            int queuedSongs = 0;
            foreach (string search in searchArr)
            {
                LavalinkLoadResult loadResult = await node.Rest.GetTracksAsync(search, LavalinkSearchType.Plain);
                bool found = false;
                foreach (LavalinkSearchType searchType in searchTypes)
                {
                    if (loadResult.LoadResultType != LavalinkLoadResultType.LoadFailed
                        && loadResult.LoadResultType != LavalinkLoadResultType.NoMatches)
                    {
                        found = true;
                        break;
                    }
                    loadResult = await node.Rest.GetTracksAsync(search, searchType);
                }
                if (found == false)
                {
                    await channel.SendMessageAsync($"{member.Mention}, track search failed for {search}");
                    int songCount = TrackCount();
                    if (conn.IsConnected && songCount < 1)
                    {
                        conn.DiscordWebSocketClosed -= DiscordWebSocketClosed;
                        conn.PlaybackStarted -= PlaybackStarted;
                        conn.PlaybackFinished -= PlaybackFinished;
                        await conn.DisconnectAsync();
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
                        conn.PlaybackFinished += PlaybackFinished;
                        conn.PlaybackStarted += PlaybackStarted;
                        conn.DiscordWebSocketClosed += DiscordWebSocketClosed;
                        await conn.PlayAsync(track);

                        //For the first song
                        AddTracks(channel.Id, member, new List<string>() { track.Uri.ToString() }, new List<string>() { track.Title }, new List<TimeSpan>() { track.Length });

                        await SendEmbedWithButtoms(channel, SongEmbed(track, "Playing", member.Username));
                    }
                    else
                    {
                        if (searchArr.Length < 2 && trackList.Count() < 2)
                        {
                            DiscordMessage messageToDelete = await channel.SendMessageAsync(SongEmbed(track, $"Queued", member.Username));
                            DataMethods.DeleteDiscordMessage(messageToDelete, TimeSpan.FromSeconds(5));
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
                DiscordMessage messageToDelete = await channel.SendMessageAsync(DataMethods.SimpleEmbed("Queued", $"{queuedSongs} {songOrSongs}"));
                DataMethods.DeleteDiscordMessage(messageToDelete, TimeSpan.FromSeconds(5));
            }
        }

        [Command("skip")]
        public async Task LavaSkip(CommandContext ctx)
        {
            await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Skip, ctx.Channel.Id);
            await ctx.Message.DeleteAsync();
        }
        [Command("pause")]
        public async Task LavaPause(CommandContext ctx)
        {
            await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Pause, ctx.Channel.Id);
            await ctx.Message.DeleteAsync();
        }
        [Command("resume")]
        [Aliases("unpause")]
        public async Task LavaResume(CommandContext ctx)
        {
            await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Resume, ctx.Channel.Id);
            await ctx.Message.DeleteAsync();
        }
        [Command("stop")]
        public async Task LavaLeave(CommandContext ctx)
        {
            await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Stop, ctx.Channel.Id);
            await ctx.Message.DeleteAsync();
        }
        public async Task VoiceActions(DiscordClient client, DiscordGuild guild, ulong userId, VoiceAction action, ulong channelId = 0)
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

            var lava = client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(guild);

            if (conn == null)
            {
                await channel.SendMessageAsync($"{member.Mention}, Lavalink is not connected.");
                return;
            }

            if (conn.CurrentState.CurrentTrack == null)
            {
                await channel.SendMessageAsync($"{member.Mention}, there are no tracks loaded.");
                return;
            }

            if (member.VoiceState == null || member.VoiceState.Channel == null)
            {
                await channel.SendMessageAsync($"{member.Mention}, you are not in a voice channel.");
                return;
            }

            if (member.VoiceState.Channel != conn.Channel)
            {
                await channel.SendMessageAsync($"{member.Mention}, the bot is in a different voice channel.");
                return;
            }

            string actionString = string.Empty;
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
                    await VoiceDisconnect(conn, "Everyone Left.");
                    return;
            }

            /*DiscordMessage messageToDelete =*/ await channel.SendMessageAsync(DataMethods.SimpleEmbed($"{actionString} by {member.Username}", $"{conn.CurrentState.CurrentTrack.Title}"));
            //DataMethods.DeleteDiscordMessage(messageToDelete, TimeSpan.FromSeconds(5));

            switch (action)
            {
                case VoiceAction.Pause:
                    await conn.PauseAsync();
                    break;
                case VoiceAction.Resume:
                    await conn.ResumeAsync();
                    break;
                case VoiceAction.Skip:
                    await conn.StopAsync();
                    break;
                case VoiceAction.Stop:
                    await VoiceDisconnect(conn);
                    break;
            }
        }

        [Command("queue")]
        public async Task LavaQueue(CommandContext ctx)
        {
            await Queue(ctx.Channel);
            await ctx.Message.DeleteAsync();
        }
        public async Task Queue(DiscordChannel channel)
        {
            int maxQueueInt = 20;
            string queueList = string.Empty;
            int maxQueue = Math.Min(TrackCount(), maxQueueInt);
            for (int i = 0; i < maxQueue; i++)
            {
                string prefix = i == 0 ? "Playing" : i.ToString();
                queueList += $"[{prefix}] - [{trackList.ElementAt(i).Length}] {trackList.ElementAt(i).Title}\n";
            }
            if (TrackCount() > maxQueueInt) queueList += $"**+ {TrackCount() - maxQueueInt} more**";
            if (queueList.Length < 1)
            {
                queueList = "No songs in queue.";
            }
            DiscordMessage messageToDelete = await channel.SendMessageAsync(DataMethods.SimpleEmbed("Queue", queueList));
            DataMethods.DeleteDiscordMessage(messageToDelete, TimeSpan.FromSeconds(30));
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
            int songCount = TrackCount();

            if (sender.IsConnected)
            {
                if (songCount > 1)
                {
                    RemoveTrack();
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
                        await channel.SendMessageAsync($"{queuedTrack.Member.Mention}, track search failed for {queuedTrack.Link}");
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
            if (reason.Length > 1)
            {
                TrackDetails queuedTrack = GetTrack();
                DiscordChannel channel = sender.Guild.GetChannel(queuedTrack.ChannelId);
                await channel.SendMessageAsync(DataMethods.SimpleEmbed($"{reason} Disconnected."));
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
            if (playing == "Playing") playingTimer = "\nEnds " + DataMethods.UnixUntil(DateTime.Now + track.Length);
            return new DiscordEmbedBuilder
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = track.Title + $" ({track.Author}) [{track.Length}]"
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
                        new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_skip", "Skip"),
                        new DiscordButtonComponent(ButtonStyle.Primary, "kb_voice_pause", "Pause"),
                        new DiscordButtonComponent(ButtonStyle.Success, "kb_voice_resume", "Resume"),
                        new DiscordButtonComponent(ButtonStyle.Danger, "kb_voice_stop", "Stop"),
                });
            builder.AddComponents(new DiscordComponent[]
                {
                        new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_queue", "Show Queue"),
                        new DiscordButtonComponent(ButtonStyle.Success, "kb_voice_retry", "Requeue This Song"),
                });
            return await channel.SendMessageAsync(builder);
        }

        private void RemoveTracks()
        {
            trackList.Clear();
        }
        private int TrackCount()
        {
            return trackList.Count;
        }
        private TrackDetails GetTrack()
        {
            return trackList.First();
        }
        private void RemoveTrack()
        {
            trackList.RemoveFirst();
        }
        private void AddTracks(ulong channelId, DiscordMember member, List<string> tracks, List<string> titles, List<TimeSpan> lengths)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                trackList.AddLast(new TrackDetails() { ChannelId = channelId, Member = member, Link = tracks[i], Title = titles[i], Length = lengths[i] });
            }
        }


        [Command("help")]
        [Cooldown(1, 2, CooldownBucketType.User)]
        public async Task Help_CC(CommandContext ctx, string commandDef = "help")
        {
            await Help(new MyContext(ctx), commandDef);
        }
        public async Task Help(MyContext ctx, string commandDef)
        {
            string command = commandDef.ToLower();

            var builder = new DiscordMessageBuilder();

            var helpEmbed = new DiscordEmbedBuilder
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = ctx.Client.CurrentUser.AvatarUrl,
                    Name = $"Kompanion | {command}",
                    Url = "https://discord.gg/ycYmMmP"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Command: {ctx.CommandName}",
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
                //case "unpause":
                //case "resume":
                default:
                    helpEmbed.AddField("Details",
                        "⮚ You can add multiple songs with the same `play` command by using a line break with the key combination \"__Shift+Enter__\".");
                    string radios = string.Empty;
                    foreach (VoiceCommands.HardLinks link in new VoiceCommands(_services).links)
                    {
                        if (link.isPublic)
                        {
                            radios += $"{link.name}, ";
                        }
                    }
                    radios = radios.Substring(0, radios.Length - 2);
                    helpEmbed.AddField("Radio Stations",
                        $"⮚ {radios}");
                    helpEmbed.AddField("Commands",
                        "```md" +
                        "\nplay [Song Name|Song Link|Playlist Link]" +
                        "\n# Play or Queue a new song or playlist." +
                        "\n``````md" +
                        "\nstop" +
                        "\n# Stop the song and disconnect." +
                        "\n``````md" +
                        "\npause" +
                        "\n# Pause the song." +
                        "\n``````md" +
                        "\nresume|unpause" +
                        "\n# Resume the song." +
                        "\n``````md" +
                        "\nskip" +
                        "\n# Skip the current song." +
                        "\n``````md" +
                        "\nqueue" +
                        "\n# Show the song queue." +
                        "\n```");
                    break;
            }
            helpEmbed.AddField(CustomStrings.embedBreak, CustomStrings.embedLinks);

            builder.WithEmbed(helpEmbed);
            await ctx.Channel.SendMessageAsync(builder);
        }
    }
}