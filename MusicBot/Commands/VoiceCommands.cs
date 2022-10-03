using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public static class VoiceCommandsExtension
    {
        public static DiscordInteractionResponseBuilder WithLoggedContent(
            this DiscordInteractionResponseBuilder builder,
            string content)
        {
            DataMethods.SendLogs(content);
            return builder.WithContent(content);
        }
        public static DiscordWebhookBuilder WithLoggedContent(
            this DiscordWebhookBuilder builder,
            string content)
        {
            DataMethods.SendLogs(content);
            return builder.WithContent(content);
        }
        public static DiscordInteractionResponseBuilder WithLog(
            this DiscordInteractionResponseBuilder builder,
            string content)
        {
            DataMethods.SendLogs(content);
            return builder;
        }
        public static DiscordWebhookBuilder WithLog(
            this DiscordWebhookBuilder builder,
            string content)
        {
            DataMethods.SendLogs(content);
            return builder;
        }
    }
    public class VoiceSlashCommands : ApplicationCommandModule
    {
        public record HardLinks
        {
            public string name;
            public string link;
        }
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
            //public ulong ChannelId;
            public DiscordMember Member;
            public string Link;
            public string Title;
            public TimeSpan Length;
        }
        public struct Lavalink
        {
            public LavalinkNodeConnection node;
            public LavalinkGuildConnection conn;
        }

        static string linksPath = "links.txt";

        static LinkedList<TrackDetails> trackList = new LinkedList<TrackDetails>();
        static Lavalink lavalink = new();

        readonly List<HardLinks> hardLinks = new();

        public VoiceSlashCommands()
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

        [SlashCommand("Play", "Play Song Name / Link")]
        public async Task Play_SC(InteractionContext ctx,
            [Option("Song", "Supports Youtube Search, Youtube, Bandcam, SoundCloud, Twitch, Vimeo, and direct HTTP audio streams")] string searchItem)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var result = await Play(ctx.Client, ctx.Guild, ctx.Member, ctx.Channel, searchItem);

            await ctx.EditResponseAsync(result);
            await Task.Delay(TimeSpan.FromSeconds(CustomStrings.messageDeleteSeconds));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("PlayFirst", "Play as the Next song")]
        public async Task PlayFirst_SC(InteractionContext ctx,
            [Option("Song", "Supports Youtube Search, Youtube, Bandcam, SoundCloud, Twitch, Vimeo, and direct HTTP audio streams")] string searchItem)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var result = await Play(ctx.Client, ctx.Guild, ctx.Member, ctx.Channel, searchItem, true);

            await ctx.EditResponseAsync(result);
            await Task.Delay(TimeSpan.FromSeconds(CustomStrings.messageDeleteSeconds));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Skip", "Skip Songs")]
        public async Task Skip_SC(InteractionContext ctx,
            [Option("Count", "Songs to Skip")] long skips = 1)
        {
            var result = await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Skip, (int)skips);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);

            await Task.Delay(TimeSpan.FromSeconds(CustomStrings.messageDeleteSeconds));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Pause", "Pause Song")]
        public async Task Pause_SC(InteractionContext ctx)
        {
            var result = await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Pause);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);

            await Task.Delay(TimeSpan.FromSeconds(CustomStrings.messageDeleteSeconds));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Resume", "Resume Song")]
        public async Task Resume_SC(InteractionContext ctx)
        {
            var result = await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Resume);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);

            await Task.Delay(TimeSpan.FromSeconds(CustomStrings.messageDeleteSeconds));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Stop", "Stop Song and Disconnect")]
        public async Task Stop_SC(InteractionContext ctx)
        {
            var result = await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Stop);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);

            await Task.Delay(TimeSpan.FromSeconds(CustomStrings.messageDeleteSeconds));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Queue", "Show Queue")]
        public async Task Queue_CC(InteractionContext ctx)
        {
            var result = Queue();
            await ctx.CreateResponseAsync(result, true);
        }
        [SlashCommand("Shortcuts", "Show this bot's shortcuts from the links.txt file")]
        public async Task Short_CC(InteractionContext ctx)
        {
            var result = Shortcuts();
            await ctx.CreateResponseAsync(result, true);
        }

        private string Shortcuts()
        {
            return hardLinks.Any() ? string.Join(", ", hardLinks.Select(t => t.name)) : "Check out [this](https://kellphy.com/musicbot) guide to add shortcut links.";
        }

        #region 1st Level
        public async Task<DiscordWebhookBuilder> Play(DiscordClient client, DiscordGuild guild, DiscordMember member, DiscordChannel channel, string searchTitles, bool playFirst = false)
        {
            if (DataMethods.staticDiscordMessage == null)
            {
                DataMethods.staticDiscordMessage = await channel.SendMessageAsync(DataMethods.SimpleEmbed("LoveLetter", "Says Hello!"));
            }

            DiscordWebhookBuilder builder = new();
            var hardLink = hardLinks.Where(t => t.name == searchTitles).FirstOrDefault();
            if (hardLink != null)
            {
                searchTitles = hardLink.link;
            }

            if (!(await InitializationAndChecks(client, guild, member)).Item1)
            {
                return builder.WithLoggedContent("Failed Initialization and Checks");
            }

            LavalinkSearchType[] searchTypes = new LavalinkSearchType[] { LavalinkSearchType.Youtube, LavalinkSearchType.SoundCloud };

            string[] searchArr = searchTitles.Split('\n').Take(100).ToArray();
            int queuedSongs = 0;
            LavalinkTrack mainTrack = new();

            foreach (string searchString in searchArr)
            {
                var search = searchString;
                if (search.Contains("spotify.com/track"))
                {
                    search = await GetTitleFromSpotify(search);
                }

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
                    return builder.WithLoggedContent($"{member.Mention}, track search failed for {search}");
                }

                List<LavalinkTrack> trackList = loadResult.Tracks.Take(100).ToList();

                if (search[..4] != "http")
                {
                    //var selectedTrack = await ChooseTrack(client, member, trackList);
                    var selectedTrack = trackList[0];
                    if (selectedTrack != null)
                    {
                        trackList = new() { selectedTrack };
                    }
                    else
                    {
                        trackList = new();
                    }
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
                        AddTracks(member, new List<string>() { track.Uri.ToString() }, new List<string>() { track.Title }, new List<TimeSpan>() { track.Length }, playFirst);

                        mainTrack = track;
                        await DataMethods.staticDiscordMessage.ModifyAsync(CreateEmbedWithButtoms(SongEmbed(track, "Playing", member.Username)));
                    }
                    else
                    {
                        tracksToQueue.Add(track.Uri.ToString());
                        trackTitles.Add(track.Title);
                        trackLengths.Add(track.Length);

                        if (searchArr.Length < 2 && trackList.Count() < 2)
                        {
                            mainTrack = track;
                            queuedSongs = -1;
                        }
                        else
                        {
                            queuedSongs++;
                        }
                    }
                }

                //For the queue
                AddTracks(member, tracksToQueue, trackTitles, trackLengths, playFirst);
            }

            //if (lavalink.conn.IsConnected && TrackCount() < 1)
            //{
            //    lavalink.conn.DiscordWebSocketClosed -= DiscordWebSocketClosed;
            //    lavalink.conn.PlaybackStarted -= PlaybackStarted;
            //    lavalink.conn.PlaybackFinished -= PlaybackFinished;
            //    await lavalink.conn.DisconnectAsync();
            //}
            if (queuedSongs > 0)
            {
                string songOrSongs = queuedSongs == 1 ? "song" : "songs";
                return builder.AddEmbed(DataMethods.SimpleEmbed("Queued", $"{queuedSongs} {songOrSongs}")).WithLog($"[Queued] {queuedSongs} {songOrSongs}");
            }
            else if (queuedSongs == -1)
            {
                return builder.AddEmbed(SongEmbed(mainTrack, $"Queued", member.Username)).WithLog($"[Queued] {mainTrack.Title}");
            }
            else
            {
                return builder.AddEmbed(SongEmbed(mainTrack, $"Added", member.Username)).WithLog($"[Added] {mainTrack.Title}");
            }
        }

        private async Task<string> GetTitleFromSpotify(string search)
        {
            string responseString;
            using (HttpClient client = new())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.102 Safari/537.36 OPR/90.0.4480.100");
                responseString = await client.GetStringAsync(search);
            }
            var regex = (new Regex("<meta property=\\\"og:title\\\" content=\\\"(?<title>.*?)\\\"/>.*<meta property=\\\"og:description\\\" content=\\\"(?<description>.*?)\\\"/>",
                RegexOptions.Singleline));
            var match = regex.Match(responseString);

            if (match.Success)
            {
                return match.Groups["title"].Value + " " + match.Groups["description"].Value;
            }

            return search;
        }

        public async Task<DiscordInteractionResponseBuilder> VoiceActions(DiscordClient client, DiscordGuild guild, ulong userId, VoiceAction action, int skips = 1, bool skipChecks = false)
        {
            DiscordInteractionResponseBuilder builder = new();

            DiscordMember member = await guild.GetMemberAsync(userId);
            if (!skipChecks && !(await InitializationAndChecks(client, guild, member)).Item1)
            {
                return builder.WithLoggedContent("Failed Initialization and Checks");
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
            }

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
                    //await EditLastMessage();
                    VoiceDisconnect(builder, lavalink.conn, "Stopped");
                    if (lavalink.conn.IsConnected)
                    {
                        await lavalink.conn.DisconnectAsync();
                        await DataMethods.staticDiscordMessage.DeleteAsync();
                        DataMethods.staticDiscordMessage = null;
                    }
                    break;
            }

            return builder.AddEmbed(DataMethods.SimpleEmbed($"{actionString}{skipString} by {member.Username}"))
                .WithLog($"[{actionString}{skipString}] by {member.Username}");
        }
        public DiscordEmbed Queue()
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
                queueList = "No songs in queue";
            }

            return DataMethods.SimpleEmbed("Queue", queueList);
        }
        #endregion

        private async Task DiscordWebSocketClosed(LavalinkGuildConnection sender, DSharpPlus.Lavalink.EventArgs.WebSocketCloseEventArgs e)
        {
            sender.DiscordWebSocketClosed -= DiscordWebSocketClosed;
            sender.PlaybackStarted -= PlaybackStarted;
            sender.PlaybackFinished -= PlaybackFinished;
            await Task.CompletedTask;
        }
        private async Task PlaybackFinished(LavalinkGuildConnection sender, DSharpPlus.Lavalink.EventArgs.TrackFinishEventArgs e)
        {
            DiscordInteractionResponseBuilder builder = new();
            await ContinueOrEnd(builder, sender);
        }
        async Task<DiscordInteractionResponseBuilder> ContinueOrEnd(DiscordInteractionResponseBuilder builder, LavalinkGuildConnection sender)
        {
            if (sender.IsConnected)
            {
                if (TrackCount() > 1)
                {
                    RemoveTracks(1);
                    var queuedTrack = trackList.First();
                    LavalinkLoadResult loadResult = await sender.Node.Rest.GetTracksAsync(queuedTrack.Link, LavalinkSearchType.Plain);
                    if (loadResult.LoadResultType != LavalinkLoadResultType.LoadFailed && loadResult.LoadResultType != LavalinkLoadResultType.NoMatches)
                    {
                        var firstTrack = loadResult.Tracks.FirstOrDefault();
                        await sender.PlayAsync(firstTrack);
                        await DataMethods.staticDiscordMessage.ModifyAsync(CreateEmbedWithButtoms(SongEmbed(firstTrack, "Playing", queuedTrack.Member.Username)));
                        return builder.WithLoggedContent($"Playback Continues with: {firstTrack.Title}");
                    }
                    else
                    {
                        builder.WithContent($"{queuedTrack.Member.Mention}, track search failed for {queuedTrack.Link}");
                        return await ContinueOrEnd(builder, sender);
                    }
                }
                else
                {
                    return builder.WithLoggedContent(VoiceDisconnect(builder, sender, "Playback Finished"));
                }
            }
            else
            {
                return builder.WithLoggedContent("Bot is disconnected");
            }
        }
        private string VoiceDisconnect(DiscordInteractionResponseBuilder builder, LavalinkGuildConnection sender, string reason)
        {
            RemoveTracks();
            sender.DiscordWebSocketClosed -= DiscordWebSocketClosed;
            sender.PlaybackStarted -= PlaybackStarted;
            sender.PlaybackFinished -= PlaybackFinished;

            DataMethods.staticDiscordMessage.ModifyAsync(new DiscordMessageBuilder().AddEmbed(DataMethods.SimpleEmbed($"{reason}")));

            return reason;
        }

        private Task PlaybackStarted(LavalinkGuildConnection sender, DSharpPlus.Lavalink.EventArgs.TrackStartEventArgs e)
        {
            return Task.CompletedTask;
        }

        #region Helpers
        public async Task<(bool, DiscordInteractionResponseBuilder)> InitializationAndChecks(DiscordClient client, DiscordGuild guild, DiscordMember member)
        {
            if ((member.VoiceState == null || member.VoiceState.Channel == null))
            {
                return (false, new DiscordInteractionResponseBuilder()
                    .WithLoggedContent($"{member.Mention}, you are not in a voice channel"));
            }

            var extension = client.GetLavalink();
            if (!extension.ConnectedNodes.Any())
            {
                return (false, new DiscordInteractionResponseBuilder()
                    .WithLoggedContent($"{member.Mention}, the Lavalink connection is not established"));
            }

            lavalink.node = extension.ConnectedNodes.Values.First();
            DiscordChannel channelVoice = member.VoiceState.Channel;
            if (lavalink.node.GetGuildConnection(channelVoice.Guild) == null)
            {
                RemoveTracks();
            }

            if ((lavalink.node.ConnectedGuilds.Count > 1 || (lavalink.node.ConnectedGuilds.Count == 1 && lavalink.node.ConnectedGuilds.First().Value.Guild.Id != guild.Id)))
            {
                return (false, new DiscordInteractionResponseBuilder()
                    .WithLoggedContent($"{member.Mention}, you are trying to connect to more than 1 server. This is not currently supported. Please launch another instance of this bot"));
            }

            await lavalink.node.ConnectAsync(channelVoice);

            lavalink.conn = lavalink.node.GetGuildConnection(guild);
            if (lavalink.conn == null)
            {
                return (false, new DiscordInteractionResponseBuilder()
                    .WithLoggedContent($"{member.Mention}, Lavalink is not connected"));
            }

            if ((member.VoiceState.Channel != lavalink.conn.Channel))
            {
                return (false, new DiscordInteractionResponseBuilder()
                    .WithLoggedContent($"{member.Mention}, the bot is in a different voice channel"));
            }
            return (true, new DiscordInteractionResponseBuilder());
        }
        DiscordEmbed SongEmbed(LavalinkTrack track, string playing, string user)
        {
            DiscordColor color;
            string playingTimer = string.Empty;
            string imageUrl = string.Empty;
            string iconUrl = string.Empty;
            string queuePrefix = string.Empty;

            if (playing == "Playing" && track.Length != TimeSpan.Zero)
            {
                playingTimer = "Ends " + DataMethods.UnixUntil(DateTime.Now + track.Length);
            }

            switch (playing)
            {
                case "Playing":
                    color = DiscordColor.Purple;
                    iconUrl = "https://m.media-amazon.com/images/G/01/digital/music/player/web/sixteen_frame_equalizer_accent.gif";
                    imageUrl = "https://mir-s3-cdn-cf.behance.net/project_modules/max_1200/a5341856722913.59bb2a94979c8.gif";
                    queuePrefix = "Songs in Queue";
                    break;
                case "Added":
                    color = DiscordColor.DarkGreen;
                    queuePrefix = "Queue";
                    break;
                case "Queued":
                    color = DiscordColor.Orange;
                    queuePrefix = "Queue";
                    break;
                default:
                    color = DiscordColor.White;
                    break;
            }

            string length = track.Length == TimeSpan.Zero ? "LIVE" : track.Length.ToString();
            string queue = TrackCount() > 1 ? $"{queuePrefix}: {TrackCount() - 1}" : string.Empty;
            string footerText = playing == "Playing" ? $"Queued by {user}\n{queue}" : queue;

            return new DiscordEmbedBuilder
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"[{length}] {track.Author}\n{track.Title}",
                    IconUrl = iconUrl
                },
                Title = $"{playingTimer}",
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = footerText
                },
                ImageUrl = imageUrl,
                Description = $"{track.Uri}",
                Color = color
            };
        }

        private DiscordMessageBuilder CreateEmbedWithButtoms(DiscordEmbed embed)
        {
            DiscordMessageBuilder builder = new();
            builder.AddEmbed(embed);
            builder.AddComponents(new DiscordComponent[]
                {
                        new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_queue", "Show Queue"),
                        new DiscordButtonComponent(ButtonStyle.Primary, "kb_voice_pause", "Pause"),
                        new DiscordButtonComponent(ButtonStyle.Success, "kb_voice_resume", "Resume"),
                        new DiscordButtonComponent(ButtonStyle.Danger, "kb_voice_stop", "Stop"),
                });
            builder.AddComponents(new DiscordComponent[]
                {
                        new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_skip", "Skip"),
                        new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_skip5", "Skip 5"),
                        new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_skip10", "Skip 10"),
                        new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_skip50", "Skip 50"),
                });

            return builder;
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
        private void AddTracks(DiscordMember member, List<string> tracks, List<string> titles, List<TimeSpan> lengths, bool playFirst)
        {
            if (!playFirst)
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    trackList.AddLast(new TrackDetails() { Member = member, Link = tracks[i], Title = titles[i], Length = lengths[i] });
                }
            }
            else
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    trackList.AddAfter(trackList.First, new TrackDetails() { Member = member, Link = tracks[i], Title = titles[i], Length = lengths[i] });
                }
            }
        }
        #endregion
    }
}