using DiscordBot.Player;
using DSharpPlus;
using DSharpPlus.Entities;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Protocol.Payloads.Events;
using Microsoft.Extensions.Options;
using MusicBot.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MusicBot.Services
{
    /// <summary>
    /// Service for managing music player instances and track queues
    /// </summary>
    public interface IPlayerManagerService
    {
        /// <summary>
        /// Gets or creates a player for the specified guild
        /// </summary>
        Task<(bool success, CustomQueuedPlayer player, string errorMessage)> GetOrCreatePlayerAsync(
            DiscordGuild guild, 
            DiscordMember member, 
            DiscordClient client);

        /// <summary>
        /// Gets the current player instance (may be null)
        /// </summary>
        CustomQueuedPlayer CurrentPlayer { get; }

        /// <summary>
        /// Gets the current status message
        /// </summary>
        DiscordMessage StatusMessage { get; set; }

        /// <summary>
        /// Disposes the current player
        /// </summary>
        Task DisposePlayerAsync();

        /// <summary>
        /// Subscribes to track events
        /// </summary>
        void SubscribeToTrackEvents(
            Func<ITrackQueueItem, Task> onTrackStarted,
            Func<ITrackQueueItem, TrackEndReason, Task> onTrackEnded);
    }

    /// <summary>
    /// Implementation of player manager service
    /// </summary>
    public class PlayerManagerService : IPlayerManagerService
    {
        private readonly IAudioService _audioService;
        private readonly ILoggingService _loggingService;
        private readonly IConfigurationService _configurationService;
        private CustomQueuedPlayer _currentPlayer;

        public CustomQueuedPlayer CurrentPlayer => _currentPlayer;
        public DiscordMessage StatusMessage { get; set; }

        public PlayerManagerService(
            IAudioService audioService,
            ILoggingService loggingService,
            IConfigurationService configurationService)
        {
            _audioService = audioService;
            _loggingService = loggingService;
            _configurationService = configurationService;
        }

        public async Task<(bool success, CustomQueuedPlayer player, string errorMessage)> GetOrCreatePlayerAsync(
            DiscordGuild guild,
            DiscordMember member,
            DiscordClient client)
        {
            // Check if member is in a voice channel
            if (member?.VoiceState == null || member.VoiceState.Channel == null)
            {
                return (false, null, $"{member?.Mention ?? "User"}, you are not in a voice channel");
            }

            var channelVoice = member.VoiceState.Channel;

            // Check if bot is already in another server
            if (_currentPlayer != null && _currentPlayer.GuildId != guild.Id)
            {
                return (false, null, $"{member.Mention}, you are trying to connect to more than 1 server. This is not currently supported. Please launch another instance of this bot");
            }

            // Check owner permissions if needed
            if (_currentPlayer == null)
            {
                var isOwner = await IsBotOwnerAsync(client, member);
                if (!isOwner && _configurationService.Configuration.IsOwnerStarted)
                {
                    return (false, null, $"{member.Mention}, the config file says that only the owner can start me :)");
                }
            }

            // Join voice channel if not connected
            if (_currentPlayer == null || _currentPlayer.State == PlayerState.Destroyed)
            {
                var result = await _audioService.Players
                    .RetrieveAsync<CustomQueuedPlayer, CustomQueuedPlayerOptions>(
                        guild.Id,
                        channelVoice.Id,
                        playerFactory: static (properties, cancellationToken) =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            return ValueTask.FromResult(new CustomQueuedPlayer(properties));
                        },
                        options: Options.Create(new CustomQueuedPlayerOptions()),
                        retrieveOptions: new PlayerRetrieveOptions(PlayerChannelBehavior.Join),
                        cancellationToken: CancellationToken.None)
                    .ConfigureAwait(false);

                _currentPlayer = result.IsSuccess ? result.Player : null;

                if (_currentPlayer == null)
                {
                    string errorMessage = result.IsSuccess 
                        ? "Failed to retrieve player" 
                        : GetPlayerErrorMessage(result.Status);
                    return (false, null, $"{member.Mention}, {errorMessage}");
                }

                _loggingService.LogInfo($"Player created for guild: {guild.Name}");
            }

            // Check if member is in the same voice channel
            if (member.VoiceState.Channel.Id != _currentPlayer.VoiceChannelId)
            {
                return (false, null, $"{member.Mention}, the bot is in a different voice channel");
            }

            return (true, _currentPlayer, string.Empty);
        }



        public async Task DisposePlayerAsync()
        {
            if (_currentPlayer != null && _currentPlayer.State != PlayerState.Destroyed)
            {
                await _currentPlayer.Queue.ClearAsync();
                await _currentPlayer.StopAsync().ConfigureAwait(false);
                await _currentPlayer.DisconnectAsync().ConfigureAwait(false);
                _currentPlayer.Dispose();
                await _currentPlayer.DisposeAsync().ConfigureAwait(false);
                _currentPlayer = null;
                _loggingService.LogInfo("Player disposed");
            }

            // Clear track requester tracker
            TrackRequesterTracker.Clear();

            if (StatusMessage != null)
            {
                try
                {
                    await StatusMessage.DeleteAsync().ConfigureAwait(false);
                }
                catch { }
                StatusMessage = null;
            }
        }

        public void SubscribeToTrackEvents(
            Func<ITrackQueueItem, Task> onTrackStarted,
            Func<ITrackQueueItem, TrackEndReason, Task> onTrackEnded)
        {
            if (_currentPlayer != null)
            {
                _currentPlayer.TrackStarted += onTrackStarted;
                _currentPlayer.TrackEnded += onTrackEnded;
            }
        }

        private string GetPlayerErrorMessage(PlayerRetrieveStatus status)
        {
            return status switch
            {
                PlayerRetrieveStatus.Success => "Success",
                PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",
                PlayerRetrieveStatus.VoiceChannelMismatch => "You are not in the same channel as the Music Bot!",
                PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected.",
                PlayerRetrieveStatus.PreconditionFailed => "An unknown error happened: Precondition Failed.",
                _ => "An unknown error happened"
            };
        }

        private Task<bool> IsBotOwnerAsync(DiscordClient client, DiscordMember member)
        {
            if (client.CurrentApplication != null)
            {
                return Task.FromResult(client.CurrentApplication.Owners.Any(x => x.Id == member.Id));
            }
            
            return Task.FromResult(member.Id == client.CurrentUser.Id);
        }
    }
}
