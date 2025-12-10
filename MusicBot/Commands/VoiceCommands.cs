using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
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

        static string linksPath = "links.txt";

        static LinkedList<TrackDetails> trackList = new LinkedList<TrackDetails>();
        static IAudioService _audioService;
        static QueuedLavalinkPlayer _player;

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
            await Task.Delay(TimeSpan.FromSeconds(CustomAttributes.messageDeleteSeconds));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("PlayFirst", "Play as the Next song")]
        public async Task PlayFirst_SC(InteractionContext ctx,
            [Option("Song", "Supports Youtube Search, Youtube, Bandcam, SoundCloud, Twitch, Vimeo, and direct HTTP audio streams")] string searchItem)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var result = await Play(ctx.Client, ctx.Guild, ctx.Member, ctx.Channel, searchItem, true);

            await ctx.EditResponseAsync(result);
            await Task.Delay(TimeSpan.FromSeconds(CustomAttributes.messageDeleteSeconds));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Skip", "Skip Songs")]
        public async Task Skip_SC(InteractionContext ctx,
            [Option("Count", "Songs to Skip")] long skips = 1)
        {
            var result = await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Skip, (int)skips);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);

            await Task.Delay(TimeSpan.FromSeconds(CustomAttributes.messageDeleteSeconds));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Pause", "Pause Song")]
        public async Task Pause_SC(InteractionContext ctx)
        {
            var result = await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Pause);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);

            await Task.Delay(TimeSpan.FromSeconds(CustomAttributes.messageDeleteSeconds));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Resume", "Resume Song")]
        public async Task Resume_SC(InteractionContext ctx)
        {
            var result = await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Resume);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);

            await Task.Delay(TimeSpan.FromSeconds(CustomAttributes.messageDeleteSeconds));
            await ctx.DeleteResponseAsync();
        }
        [SlashCommand("Stop", "Stop Song and Disconnect")]
        public async Task Stop_SC(InteractionContext ctx)
        {
            var result = await VoiceActions(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Stop);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);

            await Task.Delay(TimeSpan.FromSeconds(CustomAttributes.messageDeleteSeconds));
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
            DiscordWebhookBuilder builder = new();
            var hardLink = hardLinks.Where(t => t.name == searchTitles).FirstOrDefault();
            if (hardLink != null)
            {
                searchTitles = hardLink.link;
            }

            var initializationAndChecks = await InitializationAndChecks(client, guild, member);
            if (!initializationAndChecks.Item1)
            {
                return builder.WithLoggedContent($"Failed Initialization and Checks: {initializationAndChecks.Item2.Content}");
            }

            if (DataMethods.staticDiscordMessage == null)
            {
                DataMethods.staticDiscordMessage = await channel.SendMessageAsync(DataMethods.SimpleEmbed("LoveLetter", "Says Hello!"));
            }

            string[] searchArr = searchTitles.Split('\n').Take(100).ToArray();
            int queuedSongs = 0;
            LavalinkTrack mainTrack = null;

            foreach (string searchString in searchArr)
            {
                var search = searchString;
                if (search.Contains("spotify.com/track"))
                {
                    search = await GetTitleFromSpotify(search);
                }

                // Use Lavalink4NET search
                TrackLoadResult loadResult;
                
                // If it's a URL, try loading directly
                if (search.StartsWith("http"))
                {
                    loadResult = await _audioService.Tracks.LoadTracksAsync(search, TrackSearchMode.None);
                }
                else
                {
                    // Try YouTube search first
                    loadResult = await _audioService.Tracks.LoadTracksAsync(search, TrackSearchMode.YouTube);
                }
                
                if (!loadResult.IsSuccess || !loadResult.Tracks.Any())
                {
                    // Try SoundCloud if YouTube fails
                    loadResult = await _audioService.Tracks.LoadTracksAsync(search, TrackSearchMode.SoundCloud);
                }

                if (!loadResult.IsSuccess || !loadResult.Tracks.Any())
                {
                    return builder.WithLoggedContent($"{member.Mention}, track search failed for {search}");
                }

                List<LavalinkTrack> tracksFromSearch = loadResult.Tracks.Take(100).ToList();

                if (!search.StartsWith("http"))
                {
                    // Take first track for non-URL searches
                    var selectedTrack = tracksFromSearch[0];
                    if (selectedTrack != null)
                    {
                        tracksFromSearch = new() { selectedTrack };
                    }
                    else
                    {
                        tracksFromSearch = new();
                    }
                }

                List<string> tracksToQueue = new List<string>();
                List<string> trackTitles = new List<string>();
                List<TimeSpan> trackLengths = new List<TimeSpan>();
                foreach (LavalinkTrack track in tracksFromSearch)
                {
                    int songCount = TrackCount();
                    if (songCount < 1)
                    {
                        // Play track using Lavalink4NET player
                        await _player.PlayAsync(track);

                        //For the first song
                        AddTracks(member, new List<string>() { track.Uri.ToString() }, new List<string>() { track.Title }, new List<TimeSpan>() { track.Duration }, playFirst);

                        mainTrack = track;
                        await DataMethods.staticDiscordMessage.ModifyAsync(CreateEmbedWithButtoms(SongEmbed(track, "Playing", member.Username)));
                    }
                    else
                    {
                        tracksToQueue.Add(track.Uri.ToString());
                        trackTitles.Add(track.Title);
                        trackLengths.Add(track.Duration);

                        if (searchArr.Length < 2 && tracksFromSearch.Count() < 2)
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

            var initializationAndChecks = await InitializationAndChecks(client, guild, member);
            if (!skipChecks && !initializationAndChecks.Item1)
            {
                return builder.WithLoggedContent($"Failed Initialization and Checks: {initializationAndChecks.Item2.Content}");
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
                    await _player.PauseAsync();
                    break;
                case VoiceAction.Resume:
                    await _player.ResumeAsync();
                    break;
                case VoiceAction.Skip:
                    RemoveTracks(skips - 1);
                    await _player.SkipAsync();
                    break;
                case VoiceAction.Stop:
                    VoiceDisconnect(builder, "Stopped");
                    if (_player != null && _player.State != PlayerState.Destroyed)
                    {
                        await _player.DisconnectAsync();
                        if (DataMethods.staticDiscordMessage != null)
                        {
                            await DataMethods.staticDiscordMessage.DeleteAsync();
                            DataMethods.staticDiscordMessage = null;
                        }
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

        private async Task OnTrackEndedAsync(object sender, EventArgs eventArgs)
        {
            DiscordInteractionResponseBuilder builder = new();
            await ContinueOrEnd(builder);
        }

        async Task<DiscordInteractionResponseBuilder> ContinueOrEnd(DiscordInteractionResponseBuilder builder)
        {
            if (_player != null && _player.State != PlayerState.Destroyed)
            {
                if (TrackCount() > 1)
                {
                    RemoveTracks(1);
                    var queuedTrack = trackList.First();
                    var loadResult = await _audioService.Tracks.LoadTracksAsync(queuedTrack.Link, TrackSearchMode.None);
                    
                    if (loadResult.IsSuccess && loadResult.Tracks.Any())
                    {
                        var firstTrack = loadResult.Tracks.FirstOrDefault();
                        await _player.PlayAsync(firstTrack);
                        await DataMethods.staticDiscordMessage.ModifyAsync(CreateEmbedWithButtoms(SongEmbed(firstTrack, "Playing", queuedTrack.Member.Username)));
                        return builder.WithLoggedContent($"Playback Continues with: {firstTrack.Title}");
                    }
                    else
                    {
                        builder.WithContent($"{queuedTrack.Member.Mention}, track search failed for {queuedTrack.Link}");
                        return await ContinueOrEnd(builder);
                    }
                }
                else
                {
                    return builder.WithLoggedContent(VoiceDisconnect(builder, "Playback Finished"));
                }
            }
            else
            {
                return builder.WithLoggedContent("Bot is disconnected");
            }
        }

        private string VoiceDisconnect(DiscordInteractionResponseBuilder builder, string reason)
        {
            RemoveTracks();
            
            if (DataMethods.staticDiscordMessage != null)
            {
                DataMethods.staticDiscordMessage.ModifyAsync(new DiscordMessageBuilder().AddEmbed(DataMethods.SimpleEmbed($"{reason}")));
            }

            return reason;
        }

        #region Helpers
        public async Task<(bool, DiscordInteractionResponseBuilder)> InitializationAndChecks(DiscordClient client, DiscordGuild guild, DiscordMember member)
        {
            if ((member.VoiceState == null || member.VoiceState.Channel == null))
            {
                return (false, new DiscordInteractionResponseBuilder()
                    .WithLoggedContent($"{member.Mention}, you are not in a voice channel"));
            }

            // Get IAudioService from service provider
            if (_audioService == null)
            {
                // This should be injected properly via DI, but for now get it from a static context
                var serviceProvider = Bot.GetServiceProvider();
                _audioService = serviceProvider.GetRequiredService<IAudioService>();
            }

            DiscordChannel channelVoice = member.VoiceState.Channel;
            
            // Check if bot is already in another server
            if (_player != null && _player.GuildId != guild.Id)
            {
                return (false, new DiscordInteractionResponseBuilder()
                    .WithLoggedContent($"{member.Mention}, you are trying to connect to more than 1 server. This is not currently supported. Please launch another instance of this bot"));
            }

            // Check owner permissions if needed
            if (_player == null)
            {
                var isOwner = await IsBotOwner(client, member);
                if (!isOwner && CustomAttributes.configJson.IsOwnerStarted)
                {
                    return (false, new DiscordInteractionResponseBuilder()
                        .WithLoggedContent($"{member.Mention}, the config file says that only the owner can start me :)"));
                }
            }

            // Join voice channel if not connected
            if (_player == null || _player.State == PlayerState.Destroyed)
            {
                var result = await _audioService.Players
                    .RetrieveAsync<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions>(
                        guild.Id,
                        channelVoice.Id,
                        playerFactory: PlayerFactory.Queued,
                        options: Microsoft.Extensions.Options.Options.Create(new QueuedLavalinkPlayerOptions()),
                        retrieveOptions: new PlayerRetrieveOptions(
                            PlayerChannelBehavior.Join),
                        cancellationToken: CancellationToken.None)
                    .ConfigureAwait(false);

                _player = result.IsSuccess ? result.Player : null;

                if (_player == null)
                {
                    return (false, new DiscordInteractionResponseBuilder()
                        .WithLoggedContent($"{member.Mention}, Lavalink is not connected"));
                }
            }

            // Check if member is in the same voice channel
            if (member.VoiceState.Channel.Id != _player.VoiceChannelId)
            {
                return (false, new DiscordInteractionResponseBuilder()
                    .WithLoggedContent($"{member.Mention}, the bot is in a different voice channel"));
            }

            return (true, new DiscordInteractionResponseBuilder());
        }

        private static Task<bool> IsBotOwner(DiscordClient client, DiscordMember member)
        {
            return (client.CurrentApplication != null) ? Task.FromResult(client.CurrentApplication.Owners.Any((DiscordUser x) => x.Id == member.Id)) : Task.FromResult(member.Id == client.CurrentUser.Id);
        }

        DiscordEmbed SongEmbed(LavalinkTrack track, string playing, string user)
        {
            DiscordColor color;
            string playingTimer = string.Empty;
            string imageUrl = string.Empty;
            string iconUrl = string.Empty;
            string queuePrefix = string.Empty;

            var trackDuration = track.Duration;

            if (playing == "Playing" && trackDuration != TimeSpan.Zero)
            {
                playingTimer = "Ends " + DataMethods.UnixUntil(DateTime.Now + trackDuration);
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

            string length = trackDuration == TimeSpan.Zero ? "LIVE" : trackDuration.ToString();
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
                        new DiscordButtonComponent(ButtonStyle.Danger, "kb_voice_stop", "Disconnect"),
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
