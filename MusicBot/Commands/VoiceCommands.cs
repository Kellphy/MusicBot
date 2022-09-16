using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
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
        public static DiscordInteractionResponseBuilder WithLog(
            this DiscordInteractionResponseBuilder builder,
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
            var result = await Play(ctx.Client, ctx.Guild, ctx.Member, ctx.Channel, searchItem);

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);
            await Task.Delay(TimeSpan.FromSeconds(5));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("PlayFirst", "Play as the Next song")]
        public async Task PlayFirst_SC(InteractionContext ctx,
            [Option("Song", "Supports Youtube Search, Youtube, Bandcam, SoundCloud, Twitch, Vimeo, and direct HTTP audio streams")] string searchItem)
        {
            var result = await Play(ctx.Client, ctx.Guild, ctx.Member, ctx.Channel, searchItem, true);

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);
            await Task.Delay(TimeSpan.FromSeconds(5));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Skip", "Skip Songs")]
        public async Task Skip_SC(InteractionContext ctx,
            [Option("Count", "Songs to Skip")] long skips = 1)
        {
            var result = await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Skip, ctx.Channel.Id, (int)skips);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);

            await Task.Delay(TimeSpan.FromSeconds(5));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Pause", "Pause Song")]
        public async Task Pause_SC(InteractionContext ctx)
        {
            var result = await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Pause, ctx.Channel.Id);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);

            await Task.Delay(TimeSpan.FromSeconds(5));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Resume", "Resume Song")]
        public async Task Resume_SC(InteractionContext ctx)
        {
            var result = await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Resume, ctx.Channel.Id);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);

            await Task.Delay(TimeSpan.FromSeconds(5));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Stop", "Stop Song and Disconnect")]
        public async Task Stop_SC(InteractionContext ctx)
        {
            var result = await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Stop, ctx.Channel.Id);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);

            await Task.Delay(TimeSpan.FromSeconds(5));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Queue", "Show Queue")]
        public async Task Queue_CC(InteractionContext ctx)
        {
            var result = Queue();
            await ctx.CreateResponseAsync(result, true);
        }

        #region 1st Level
        public async Task<DiscordInteractionResponseBuilder> Play(DiscordClient client, DiscordGuild guild, DiscordMember member, DiscordChannel channel, string searchTitles, bool playFirst = false)
        {
            if (DataMethods.staticDiscordMessage == null)
            {
                DataMethods.staticDiscordMessage = await channel.SendMessageAsync(DataMethods.SimpleEmbed("Music Bot", "Initialized"));
            }

            DiscordInteractionResponseBuilder builder = new();
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
        public async Task<DiscordInteractionResponseBuilder> VoiceActions(DiscordClient client, DiscordGuild guild, ulong userId, VoiceAction action, ulong channelId, int skips = 1, bool skipChecks = false)
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

        //private async Task<LavalinkTrack> ChooseTrack(DiscordClient client, DiscordMember member, List<LavalinkTrack> trackList)
        //{
        //    #region Emoji 1-10
        //    DiscordEmoji[] emojiOptions = {
        //        DiscordEmoji.FromName(client, ":regional_indicator_a:"),
        //        DiscordEmoji.FromName(client, ":regional_indicator_b:"),
        //        DiscordEmoji.FromName(client, ":regional_indicator_c:"),
        //        DiscordEmoji.FromName(client, ":regional_indicator_d:"),
        //        DiscordEmoji.FromName(client, ":regional_indicator_e:"),
        //        };
        //    #endregion
        //    var description = string.Empty;
        //    for (int z = 0; z < Math.Min(trackList.Count, emojiOptions.Length); z++)
        //    {
        //        string length = trackList[z].Length == TimeSpan.Zero ? "LIVE" : trackList[z].Length.ToString();

        //        description = string.Concat(description,
        //             $"```[{emojiOptions[z]}] [{length}] {trackList[z].Title} ({trackList[z].Author})\n```");
        //    }
        //    var embed = DataMethods.SimpleEmbed("Choose a track", description);
        //    var message = await channel.SendMessageAsync(embed);

        //    for (int z = 0; z < Math.Min(trackList.Count, emojiOptions.Length); z++)
        //    {
        //        await message.CreateReactionAsync(emojiOptions[z]);
        //    }
        //    await message.CreateReactionAsync(DiscordEmoji.FromName(client, ":x:"));

        //    var interaction = await client.GetInteractivity().WaitForReactionAsync(message, member, TimeSpan.FromMinutes(2));
        //    if (!interaction.TimedOut && emojiOptions.Contains(interaction.Result.Emoji))
        //    {
        //        int index = Array.IndexOf(emojiOptions, interaction.Result.Emoji);
        //        await message.DeleteAsync();
        //        return trackList[index];
        //    }
        //    else
        //    {
        //        await message.DeleteAsync();
        //        return null;
        //    }
        //}
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
            //await EditLastMessage();

            if (sender.IsConnected)
            {
                if (TrackCount() > 1)
                {
                    RemoveTracks(1);
                    var queuedTrack = trackList.First();
                    LavalinkLoadResult loadResult = await sender.Node.Rest.GetTracksAsync(queuedTrack.Link, LavalinkSearchType.Plain);
                    if (loadResult.LoadResultType != LavalinkLoadResultType.LoadFailed && loadResult.LoadResultType != LavalinkLoadResultType.NoMatches)
                    {
                        await sender.PlayAsync(loadResult.Tracks.FirstOrDefault());
                        await DataMethods.staticDiscordMessage.ModifyAsync(CreateEmbedWithButtoms(SongEmbed(loadResult.Tracks.FirstOrDefault(), "Playing", queuedTrack.Member.Username)));
                    }
                    else
                    {
                        builder.WithContent($"{queuedTrack.Member.Mention}, track search failed for {queuedTrack.Link}");
                        return await ContinueOrEnd(builder, sender);
                    }
                }
                else
                {
                    VoiceDisconnect(builder, sender, "Playback Finished");
                }
            }

            return builder.WithLoggedContent("Bot is disconnected");
        }
        private void VoiceDisconnect(DiscordInteractionResponseBuilder builder, LavalinkGuildConnection sender, string reason)
        {
            RemoveTracks();
            sender.DiscordWebSocketClosed -= DiscordWebSocketClosed;
            sender.PlaybackStarted -= PlaybackStarted;
            sender.PlaybackFinished -= PlaybackFinished;

            DataMethods.SendLogs($"{reason} Detached");

            DataMethods.staticDiscordMessage.ModifyAsync(new DiscordMessageBuilder().AddEmbed(DataMethods.SimpleEmbed($"{reason}")));
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
            string playingTimer = string.Empty;
            if (playing == "Playing" && track.Length != TimeSpan.Zero)
            {
                playingTimer = "\nEnds " + DataMethods.UnixUntil(DateTime.Now + track.Length);
            }
            string length = track.Length == TimeSpan.Zero ? "LIVE" : track.Length.ToString();
            DiscordColor color;
            string imageUrl = string.Empty;
            string iconUrl = string.Empty;
            switch (playing)
            {
                case "Playing":
                    color = DiscordColor.Purple;
                    iconUrl = "https://m.media-amazon.com/images/G/01/digital/music/player/web/sixteen_frame_equalizer_accent.gif";
                    imageUrl = "https://mir-s3-cdn-cf.behance.net/project_modules/max_1200/a5341856722913.59bb2a94979c8.gif";
                    break;
                case "Added":
                    color = DiscordColor.DarkGreen;
                    break;
                case "Queued":
                    color = DiscordColor.Orange;
                    break;
                default:
                    color = DiscordColor.White;
                    break;
            }
            return new DiscordEmbedBuilder
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = track.Title + $" ({track.Author})",
                    IconUrl = iconUrl
                },
                Title = $"[{length}] " + $"{playingTimer}",
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Queued by {user}"
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

            return builder/*.WithLog($"[Playing] {embed.Author.Name}")*/;
        }
        //private async Task EditLastMessage()
        //{
        //    if (message != null)
        //    {
        //        try
        //        {
        //            DiscordMessageBuilder builder = new DiscordMessageBuilder();
        //            DiscordEmbed embed = message.Embeds.FirstOrDefault();

        //            DiscordEmbedBuilder newEmbed = new DiscordEmbedBuilder
        //            {
        //                Author = new DiscordEmbedBuilder.EmbedAuthor
        //                {
        //                    Name = embed.Author.Name
        //                },
        //                Description = embed.Description,
        //                Color = DiscordColor.VeryDarkGray
        //            };

        //            builder.WithEmbed(newEmbed);
        //            builder.AddComponents(new DiscordComponent[]
        //                {
        //                new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_retry", "Requeue This Song"),
        //                });

        //            await message.ModifyAsync(builder);
        //        }
        //        catch
        //        {
        //            DataMethods.SendErrorLogs("Could not update last message");
        //        }
        //    }
        //}
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