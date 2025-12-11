using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.DependencyInjection;
using MusicBot.Extensions;
using MusicBot.Models;
using MusicBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    /// <summary>
    /// Voice action types for player control
    /// </summary>
    public enum VoiceAction
    {
        None,
        Skip,
        Pause,
        Resume,
        Stop,
        Disconnect
    }

    /// <summary>
    /// Slash commands for music playback control
    /// </summary>
    public class VoiceSlashCommands : ApplicationCommandModule
    {
        private IPlayerManagerService _playerManager;
        private IMusicService _musicService;
        private IEmbedService _embedService;
        private ILoggingService _loggingService;
        private IShortcutService _shortcutService;
        private IProgressTrackerService _progressTracker;
        private IButtonService _buttonService;

        /// <summary>
        /// Initializes services from dependency injection
        /// </summary>
        private void InitializeServices(InteractionContext ctx)
        {
            var services = ctx.Services;
            _playerManager ??= services.GetRequiredService<IPlayerManagerService>();
            _musicService ??= services.GetRequiredService<IMusicService>();
            _embedService ??= services.GetRequiredService<IEmbedService>();
            _loggingService ??= services.GetRequiredService<ILoggingService>();
            _shortcutService ??= services.GetRequiredService<IShortcutService>();
            _progressTracker ??= services.GetRequiredService<IProgressTrackerService>();
            _buttonService ??= services.GetRequiredService<IButtonService>();
        }

        [SlashCommand("Play", "Play Song Name / Link")]
        public async Task Play_SC(InteractionContext ctx,
            [Option("Song", "Supports Youtube Search, Youtube, Bandcamp, SoundCloud, Twitch, Vimeo, and direct HTTP audio streams")] string searchItem)
        {
            InitializeServices(ctx);
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var result = await PlayAsync(ctx.Client, ctx.Guild, ctx.Member, ctx.Channel, searchItem);
            await ctx.EditResponseAsync(result);
            await ctx.DeleteAfterDelayAsync(BotConstants.MessageDeleteSeconds);
        }

        [SlashCommand("PlayNext", "Play as the Next song")]
        public async Task PlayNext_SC(InteractionContext ctx,
            [Option("Song", "Supports Youtube Search, Youtube, Bandcamp, SoundCloud, Twitch, Vimeo, and direct HTTP audio streams")] string searchItem)
        {
            InitializeServices(ctx);
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var result = await PlayAsync(ctx.Client, ctx.Guild, ctx.Member, ctx.Channel, searchItem, playFirst: true);
            await ctx.EditResponseAsync(result);
            await ctx.DeleteAfterDelayAsync(BotConstants.MessageDeleteSeconds);
        }

        [SlashCommand("Skip", "Skip Songs")]
        public async Task Skip_SC(InteractionContext ctx,
            [Option("Count", "Songs to Skip")] long skips = 1)
        {
            InitializeServices(ctx);
            var result = await VoiceActionAsync(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Skip, (int)skips);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);
            await ctx.DeleteAfterDelayAsync(BotConstants.MessageDeleteSeconds);
        }

        [SlashCommand("Pause", "Pause Song")]
        public async Task Pause_SC(InteractionContext ctx)
        {
            InitializeServices(ctx);
            var result = await VoiceActionAsync(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Pause);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);
            await ctx.DeleteAfterDelayAsync(BotConstants.MessageDeleteSeconds);
        }

        [SlashCommand("Resume", "Resume Song")]
        public async Task Resume_SC(InteractionContext ctx)
        {
            InitializeServices(ctx);
            var result = await VoiceActionAsync(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Resume);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);
            await ctx.DeleteAfterDelayAsync(BotConstants.MessageDeleteSeconds);
        }

        [SlashCommand("Stop", "Stop Song and Disconnect")]
        public async Task Stop_SC(InteractionContext ctx)
        {
            InitializeServices(ctx);
            var result = await VoiceActionAsync(ctx.Client, ctx.Guild, ctx.User.Id, VoiceAction.Stop);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, result);
            await ctx.DeleteAfterDelayAsync(BotConstants.MessageDeleteSeconds);
        }

        [SlashCommand("Queue", "Show Queue")]
        public async Task Queue_SC(InteractionContext ctx)
        {
            InitializeServices(ctx);
            var result = GetQueueEmbed();
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(result).AsEphemeral());
        }

        [SlashCommand("Shortcuts", "Show this bot's shortcuts from the links.txt file")]
        public async Task Shortcuts_SC(InteractionContext ctx)
        {
            InitializeServices(ctx);
            var shortcutsDisplay = _shortcutService.GetShortcutsDisplay();
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent(shortcutsDisplay)
                    .AsEphemeral());
        }

        [SlashCommand("Seek", "Seek to a specific position (1:30) or relative (+30, -15)")]
        public async Task Seek_SC(InteractionContext ctx,
            [Option("Time", "Time: mm:ss, hh:mm:ss, +mm:ss (forward), -mm:ss (backward)")] string timeString)
        {
            InitializeServices(ctx);
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var result = await SeekAsync(ctx.Client, ctx.Guild, ctx.Member, timeString);
            await ctx.EditResponseAsync(result);
            await ctx.DeleteAfterDelayAsync(BotConstants.MessageDeleteSeconds);
        }

        [SlashCommand("Remove", "Remove a track from the queue")]
        public async Task Remove_SC(InteractionContext ctx,
            [Option("Position", "Track position to remove (1-based index)")] long position)
        {
            InitializeServices(ctx);
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var result = await RemoveTrackAsync(ctx.Client, ctx.Guild, ctx.Member, (int)position);
            await ctx.EditResponseAsync(result);
            await ctx.DeleteAfterDelayAsync(BotConstants.MessageDeleteSeconds);
        }

        #region Command Implementation Methods

        private async Task<DiscordWebhookBuilder> PlayAsync(
            DiscordClient client,
            DiscordGuild guild,
            DiscordMember member,
            DiscordChannel channel,
            string searchTitles,
            bool playFirst = false)
        {
            var builder = new DiscordWebhookBuilder();

            if (string.IsNullOrWhiteSpace(searchTitles))
            {
                return builder.WithLoggedContent($"{member.Mention}, search query cannot be empty", _loggingService);
            }

            // Resolve shortcuts
            searchTitles = _shortcutService.ResolveShortcut(searchTitles);

            // Get or create player
            var (success, player, errorMessage) = await _playerManager.GetOrCreatePlayerAsync(guild, member, client);
            if (!success)
            {
                return builder.WithLoggedContent($"Failed: {errorMessage}", _loggingService);
            }

            // Subscribe to events if not already subscribed
            if (!_progressTracker.IsTracking)
            {
                _playerManager.SubscribeToTrackEvents(OnTrackStartedAsync, OnTrackEndedAsync);
            }

            // Create status message if doesn't exist
            if (_playerManager.StatusMessage == null)
            {
                _playerManager.StatusMessage = await channel.SendMessageAsync(
                    _embedService.CreateSimpleEmbed("LoveLetter", "Says Hello!"));
            }

            // Split search queries (support multiple lines)
            string[] searchArr = searchTitles.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Take(BotConstants.MaxTracksPerCommand).ToArray();

            int queuedSongs = 0;
            LavalinkTrack mainTrack = null;

            foreach (string searchString in searchArr)
            {
                var search = searchString;

                // Convert Spotify URLs
                if (search.Contains("spotify.com/track"))
                {
                    search = await _musicService.ConvertSpotifyUrlAsync(search);
                }

                // Load tracks
                var (loadSuccess, tracks, loadError) = await _musicService.LoadTracksAsync(search);
                if (!loadSuccess || tracks == null || !tracks.Any())
                {
                    return builder.WithLoggedContent($"{member.Mention}, {loadError}", _loggingService);
                }

                // Process tracks
                var result = await ProcessTracksAsync(member, tracks, mainTrack, queuedSongs, playFirst, searchArr.Length);
                mainTrack = result.mainTrack;
                queuedSongs = result.queuedSongs;
            }

            return BuildPlayResponse(builder, mainTrack, queuedSongs, member.Username);
        }

        private async Task<(LavalinkTrack mainTrack, int queuedSongs)> ProcessTracksAsync(
            DiscordMember member,
            List<LavalinkTrack> tracks,
            LavalinkTrack mainTrack,
            int queuedSongs,
            bool playFirst,
            int searchCount)
        {
            var player = _playerManager.CurrentPlayer;
            bool isFirstTrack = player.CurrentTrack == null;

            foreach (var track in tracks)
            {
                var queueItem = new TrackQueueItem(track);
                TrackRequesterTracker.SetRequester(track.Identifier, member.Username);

                if (isFirstTrack && mainTrack == null)
                {
                    // First track - play it immediately
                    await player.PlayAsync(track);
                    mainTrack = track;
                    isFirstTrack = false;

                    // Update status message for first track
                    await UpdateStatusMessageAsync(track, member.Username);
                }
                else
                {
                    // Queue subsequent tracks
                    if (playFirst)
                    {
                        // Insert at the beginning for PlayNext
                        await player.Queue.InsertAsync(0, queueItem);
                    }
                    else
                    {
                        // Add to the end for normal Play
                        await player.Queue.AddAsync(queueItem);
                    }

                    if (searchCount < 2 && tracks.Count < 2)
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

            return (mainTrack, queuedSongs);
        }

        private async Task UpdateStatusMessageAsync(LavalinkTrack track, string requestedBy)
        {
            var player = _playerManager.CurrentPlayer;
            int queueCount = player?.Queue.Count ?? 0;
            
            var mainEmbed = _embedService.CreateTrackEmbed(track, "Playing", requestedBy, queueCount);

            var timeFormat = track.Duration.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss";
            var positionStr = TimeSpan.Zero.ToString(timeFormat);
            var durationStr = track.Duration.ToString(timeFormat);
            var fixedChars = positionStr.Length + durationStr.Length + " [] -".Length;
            var dynamicBarLength = BotConstants.DefaultProgressBarLength - fixedChars;

            var progressEmbed = _embedService.CreateProgressEmbed(TimeSpan.Zero, track.Duration, dynamicBarLength);
            var messageBuilder = _buttonService.CreateMessageWithButtons(mainEmbed, progressEmbed);

            await _playerManager.StatusMessage.ModifyAsync(messageBuilder);

            // Start progress tracking
            _progressTracker.StartTracking(mainEmbed);
        }

        private DiscordWebhookBuilder BuildPlayResponse(
            DiscordWebhookBuilder builder,
            LavalinkTrack mainTrack,
            int queuedSongs,
            string username)
        {
            if (queuedSongs > 0)
            {
                string songOrSongs = queuedSongs == 1 ? "song" : "songs";
                return builder.AddEmbed(_embedService.CreateSimpleEmbed("Queued", $"{queuedSongs} {songOrSongs}"))
                    .WithLog($"[Queued] {queuedSongs} {songOrSongs}", _loggingService);
            }
            else if (queuedSongs == -1)
            {
                return builder.AddEmbed(_embedService.CreateTrackEmbed(mainTrack, "Queued", username))
                    .WithLog($"[Queued] {mainTrack.Title}", _loggingService);
            }
            else
            {
                return builder.AddEmbed(_embedService.CreateTrackEmbed(mainTrack, "Added", username))
                    .WithLog($"[Added] {mainTrack.Title}", _loggingService);
            }
        }

        private async Task<DiscordInteractionResponseBuilder> VoiceActionAsync(
            DiscordClient client,
            DiscordGuild guild,
            ulong userId,
            VoiceAction action,
            int skips = 1,
            bool skipChecks = false)
        {
            var builder = new DiscordInteractionResponseBuilder();
            var member = await guild.GetMemberAsync(userId);

            if (!skipChecks)
            {
                var (success, player, errorMessage) = await _playerManager.GetOrCreatePlayerAsync(guild, member, client);
                if (!success)
                {
                    return builder.WithLoggedContent($"Failed: {errorMessage}", _loggingService);
                }
            }

            var actionString = GetActionString(action);
            var skipString = skips > 1 ? $" {skips}" : string.Empty;

            await ExecuteActionAsync(action, skips);

            return builder.AddEmbed(_embedService.CreateSimpleEmbed($"{actionString}{skipString} by {member.Username}"))
                .WithLog($"[{actionString}{skipString}] by {member.Username}", _loggingService);
        }

        private async Task ExecuteActionAsync(VoiceAction action, int skips)
        {
            var player = _playerManager.CurrentPlayer;
            if (player == null) return;

            switch (action)
            {
                case VoiceAction.Pause:
                    await player.PauseAsync().ConfigureAwait(false);
                    break;

                case VoiceAction.Resume:
                    await player.ResumeAsync().ConfigureAwait(false);
                    break;

                case VoiceAction.Skip:
                    // Use library's SkipAsync with count parameter
                    await player.SkipAsync(skips).ConfigureAwait(false);
                    break;

                case VoiceAction.Stop:
                    _progressTracker.StopTracking();
                    await _playerManager.DisposePlayerAsync();
                    break;
            }
        }

        private string GetActionString(VoiceAction action)
        {
            return action switch
            {
                VoiceAction.Pause => "Paused",
                VoiceAction.Resume => "Resumed",
                VoiceAction.Skip => "Skipped",
                VoiceAction.Stop => "Stopped",
                _ => string.Empty
            };
        }

        public DiscordEmbed GetQueueEmbedPublic()
        {
            // Initialize services if needed
            if (_playerManager == null)
            {
                var services = Bot.GetServiceProvider();
                _playerManager = services.GetRequiredService<IPlayerManagerService>();
                _embedService = services.GetRequiredService<IEmbedService>();
            }

            var player = _playerManager.CurrentPlayer;
            if (player == null)
            {
                return _embedService.CreateSimpleEmbed("Queue", "No active player");
            }

            var queue = player.Queue;
            
            // Check if there are any tracks in queue (excluding currently playing)
            if (queue.Count == 0)
            {
                return _embedService.CreateSimpleEmbed("Queue", "No upcoming songs in queue");
            }

            var queueList = string.Empty;

            // Show upcoming tracks from queue
            var maxQueue = Math.Min(queue.Count, BotConstants.MaxQueueDisplayCount);
            for (int i = 0; i < maxQueue; i++)
            {
                var queueItem = queue[i];
                var track = queueItem.Track;
                string trackLength = track.Duration == TimeSpan.Zero ? "LIVE" : track.Duration.ToString(@"mm\:ss");
                string trackRequestedBy = TrackRequesterTracker.GetRequester(track.Identifier);
                
                queueList += $"```\n[{i + 1}] - [{trackLength}] {track.Title} - By {trackRequestedBy}\n```";
            }

            if (queue.Count > BotConstants.MaxQueueDisplayCount)
            {
                queueList += $"**+ {queue.Count - BotConstants.MaxQueueDisplayCount} more**";
            }

            return _embedService.CreateSimpleEmbed("Queue", queueList);
        }

        private DiscordEmbed GetQueueEmbed()
        {
            return GetQueueEmbedPublic();
        }

        private async Task<DiscordWebhookBuilder> SeekAsync(
            DiscordClient client,
            DiscordGuild guild,
            DiscordMember member,
            string timeString)
        {
            var builder = new DiscordWebhookBuilder();

            var (success, player, errorMessage) = await _playerManager.GetOrCreatePlayerAsync(guild, member, client);
            if (!success)
            {
                return builder.WithLoggedContent($"Failed: {errorMessage}", _loggingService);
            }

            if (player.CurrentItem?.Track == null)
            {
                return builder.WithLoggedContent($"{member.Mention}, no track is currently playing", _loggingService);
            }

            // Parse seek time
            bool isRelative = timeString.StartsWith("+") || timeString.StartsWith("-");
            bool isForward = timeString.StartsWith("+");
            string timeValue = isRelative ? timeString.Substring(1) : timeString;

            if (!_musicService.TryParseTimeString(timeValue, out TimeSpan parsedTime))
            {
                return builder.WithLoggedContent(
                    $"{member.Mention}, invalid time format. Use mm:ss, hh:mm:ss, +mm:ss, or -mm:ss",
                    _loggingService);
            }

            var currentPosition = player.Position?.Position ?? TimeSpan.Zero;
            TimeSpan seekTime = isRelative
                ? (isForward ? currentPosition + parsedTime : (currentPosition - parsedTime > TimeSpan.Zero ? currentPosition - parsedTime : TimeSpan.Zero))
                : parsedTime;

            var trackDuration = player.CurrentTrack.Duration;
            if (trackDuration != TimeSpan.Zero && seekTime > trackDuration)
            {
                return builder.WithLoggedContent(
                    $"{member.Mention}, seek time exceeds track duration ({trackDuration:mm\\:ss})",
                    _loggingService);
            }

            await player.SeekAsync(seekTime).ConfigureAwait(false);

            string seekMessage = isRelative
                ? $"{(isForward ? "Forward" : "Backward")} {parsedTime:mm\\:ss} to {seekTime:mm\\:ss}"
                : $"Jumped to {seekTime:mm\\:ss}";

            return builder.AddEmbed(_embedService.CreateSimpleEmbed("Seeked", seekMessage))
                .WithLog($"[Seeked] {seekMessage}", _loggingService);
        }

        private async Task<DiscordWebhookBuilder> RemoveTrackAsync(
            DiscordClient client,
            DiscordGuild guild,
            DiscordMember member,
            int position)
        {
            var builder = new DiscordWebhookBuilder();
            var player = _playerManager.CurrentPlayer;
            
            if (player == null)
            {
                return builder.WithLoggedContent(
                    $"{member.Mention}, no active player",
                    _loggingService);
            }

            var queue = player.Queue;

            if (position < 1 || position > queue.Count)
            {
                return builder.WithLoggedContent(
                    $"{member.Mention}, invalid position. Queue has {queue.Count} track(s)",
                    _loggingService);
            }

            // Get track info before removing (convert to 0-based index)
            var trackToRemove = queue[position - 1].Track;
            
            // Use Lavalink's built-in RemoveAtAsync
            await player.Queue.RemoveAtAsync(position - 1);

            return builder.AddEmbed(_embedService.CreateSimpleEmbed("Removed", $"Removed: {trackToRemove.Title}"))
                .WithLog($"[Removed] {trackToRemove.Title}", _loggingService);
        }

        #endregion

        #region Button Handlers

        /// <summary>
        /// Handles voice action button clicks from Discord interactions
        /// </summary>
        public async Task HandleVoiceActionFromButton(DSharpPlus.EventArgs.ComponentInteractionCreateEventArgs e, DiscordClient client, VoiceAction action, int skips = 1)
        {
            try
            {
                // Initialize services if needed
                if (_playerManager == null)
                {
                    var services = Bot.GetServiceProvider();
                    _playerManager = services.GetRequiredService<IPlayerManagerService>();
                    _musicService = services.GetRequiredService<IMusicService>();
                    _embedService = services.GetRequiredService<IEmbedService>();
                    _loggingService = services.GetRequiredService<ILoggingService>();
                    _shortcutService = services.GetRequiredService<IShortcutService>();
                    _progressTracker = services.GetRequiredService<IProgressTrackerService>();
                    _buttonService = services.GetRequiredService<IButtonService>();
                }

                // Acknowledge the interaction immediately
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

                // Use the same logic as slash commands
                var result = await VoiceActionAsync(client, e.Guild, e.User.Id, action, skips);

                // Send visible follow-up message that auto-deletes
                var followup = await e.Interaction.CreateFollowupMessageAsync(
                    new DiscordFollowupMessageBuilder()
                        .AddEmbeds(result.Embeds));

                // Delete after delay
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(BotConstants.MessageDeleteSeconds));
                    try { await followup.DeleteAsync(); } catch { }
                });
            }
            catch (Exception ex)
            {
                _loggingService?.LogError($"HandleVoiceActionFromButton error: {ex.Message}");
                try
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent($"Error executing action: {ex.Message}")
                            .AsEphemeral());
                }
                catch
                {
                    try
                    {
                        await e.Interaction.CreateFollowupMessageAsync(
                            new DiscordFollowupMessageBuilder()
                                .WithContent($"Error executing action: {ex.Message}")
                                .AsEphemeral());
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Handles seek button clicks from Discord interactions
        /// </summary>
        public async Task HandleSeekFromButton(DSharpPlus.EventArgs.ComponentInteractionCreateEventArgs e, DiscordClient client, string timeString)
        {
            try
            {
                // Initialize services if needed
                if (_playerManager == null)
                {
                    var services = Bot.GetServiceProvider();
                    _playerManager = services.GetRequiredService<IPlayerManagerService>();
                    _musicService = services.GetRequiredService<IMusicService>();
                    _embedService = services.GetRequiredService<IEmbedService>();
                    _loggingService = services.GetRequiredService<ILoggingService>();
                    _shortcutService = services.GetRequiredService<IShortcutService>();
                    _progressTracker = services.GetRequiredService<IProgressTrackerService>();
                    _buttonService = services.GetRequiredService<IButtonService>();
                }

                // Acknowledge the interaction immediately
                await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

                var member = await e.Guild.GetMemberAsync(e.User.Id);

                // Use the same logic as slash commands
                var result = await SeekAsync(client, e.Guild, member, timeString);

                // Send visible follow-up message that auto-deletes
                var followup = await e.Interaction.CreateFollowupMessageAsync(
                    new DiscordFollowupMessageBuilder()
                        .AddEmbeds(result.Embeds));

                // Delete after delay
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(BotConstants.MessageDeleteSeconds));
                    try { await followup.DeleteAsync(); } catch { }
                });
            }
            catch (Exception ex)
            {
                _loggingService?.LogError($"HandleSeekFromButton error: {ex.Message}");
                try
                {
                    await e.Interaction.CreateResponseAsync(
                        InteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder()
                            .WithContent($"Error seeking: {ex.Message}")
                            .AsEphemeral());
                }
                catch
                {
                    try
                    {
                        await e.Interaction.CreateFollowupMessageAsync(
                            new DiscordFollowupMessageBuilder()
                                .WithContent($"Error seeking: {ex.Message}")
                                .AsEphemeral());
                    }
                    catch { }
                }
            }
        }

        #endregion

        #region Event Handlers

        private async Task OnTrackStartedAsync(ITrackQueueItem trackQueueItem)
        {
            try
            {
                if (trackQueueItem?.Track == null || _playerManager.StatusMessage == null)
                    return;

                var track = trackQueueItem.Track;
                string requester = TrackRequesterTracker.GetRequester(track.Identifier);

                await UpdateStatusMessageAsync(track, requester);
                _loggingService.LogInfo($"Track started: {track.Title}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"OnTrackStartedAsync error: {ex.Message}");
            }
        }

        private async Task OnTrackEndedAsync(ITrackQueueItem trackQueueItem, TrackEndReason endReason)
        {
            try
            {
                _loggingService.LogInfo($"Track ended: {trackQueueItem?.Track?.Title ?? "Unknown"}, Reason: {endReason}");

                if (endReason != TrackEndReason.Replaced)
                {
                    _progressTracker.StopTracking();
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"OnTrackEndedAsync error: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        #endregion
    }
}
