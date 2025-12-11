using DSharpPlus.Entities;
using Lavalink4NET.Players;
using MusicBot.Models;
using System;
using System.Threading;

namespace MusicBot.Services
{
    /// <summary>
    /// Service for managing progress bar updates for playing tracks
    /// </summary>
    public interface IProgressTrackerService
    {
        /// <summary>
        /// Starts tracking progress for the current track
        /// </summary>
        void StartTracking(DiscordEmbed mainEmbed);

        /// <summary>
        /// Stops tracking progress
        /// </summary>
        void StopTracking();

        /// <summary>
        /// Gets if progress tracking is currently active
        /// </summary>
        bool IsTracking { get; }
    }

    /// <summary>
    /// Implementation of progress tracker service
    /// </summary>
    public class ProgressTrackerService : IProgressTrackerService
    {
        private readonly IPlayerManagerService _playerManager;
        private readonly IEmbedService _embedService;
        private readonly ILoggingService _loggingService;
        private readonly IButtonService _buttonService;

        private Timer _progressTimer;
        private DateTime _trackStartTime;
        private bool _isUpdating;
        private DiscordEmbed _currentMainEmbed;

        public bool IsTracking => _progressTimer != null;

        public ProgressTrackerService(
            IPlayerManagerService playerManager,
            IEmbedService embedService,
            ILoggingService loggingService,
            IButtonService buttonService)
        {
            _playerManager = playerManager;
            _embedService = embedService;
            _loggingService = loggingService;
            _buttonService = buttonService;
        }

        public void StartTracking(DiscordEmbed mainEmbed)
        {
            StopTracking();
            _currentMainEmbed = mainEmbed;
            _trackStartTime = DateTime.Now;
            _progressTimer = new Timer(
                UpdateProgressCallback,
                null,
                TimeSpan.FromSeconds(BotConstants.ProgressUpdateIntervalSeconds),
                TimeSpan.FromSeconds(BotConstants.ProgressUpdateIntervalSeconds));
        }

        public void StopTracking()
        {
            _progressTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _progressTimer?.Dispose();
            _progressTimer = null;
        }

        private async void UpdateProgressCallback(object state)
        {
            if (_isUpdating)
                return;

            var player = _playerManager.CurrentPlayer;
            if (player == null || player.State != PlayerState.Playing)
                return;

            try
            {
                _isUpdating = true;

                var currentTrack = player.CurrentTrack;
                var statusMessage = _playerManager.StatusMessage;

                if (currentTrack == null || statusMessage == null)
                    return;

                var position = player.Position?.Position ?? (DateTime.Now - _trackStartTime);
                var duration = currentTrack.Duration;

                if (duration == TimeSpan.Zero || position > duration)
                    return;

                // Calculate dynamic progress bar length
                var timeFormat = duration.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss";
                var positionStr = position.ToString(timeFormat);
                var remainingStr = (duration - position).ToString(timeFormat);
                var fixedChars = positionStr.Length + remainingStr.Length + " [] -".Length;
                var dynamicBarLength = BotConstants.DefaultProgressBarLength - fixedChars;

                var progressEmbed = _embedService.CreateProgressEmbed(position, duration, dynamicBarLength);
                var messageBuilder = _buttonService.CreateMessageWithButtons(_currentMainEmbed, progressEmbed);

                await statusMessage.ModifyAsync(messageBuilder);
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Progress update error: {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
}
