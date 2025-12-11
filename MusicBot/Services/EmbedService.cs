using DSharpPlus.Entities;
using Lavalink4NET.Tracks;
using System;

namespace MusicBot.Services
{
    /// <summary>
    /// Service for creating Discord embeds with consistent styling
    /// </summary>
    public interface IEmbedService
    {
        /// <summary>
        /// Creates a simple embed with title and description
        /// </summary>
        DiscordEmbed CreateSimpleEmbed(string title = null, string description = null, DiscordColor? color = null);

        /// <summary>
        /// Creates an embed for a track with status
        /// </summary>
        DiscordEmbed CreateTrackEmbed(LavalinkTrack track, string status, string requestedBy, int queueCount = 0);

        /// <summary>
        /// Creates a progress bar embed for a playing track
        /// </summary>
        DiscordEmbed CreateProgressEmbed(TimeSpan position, TimeSpan duration, int barLength);

        /// <summary>
        /// Converts a DateTime to Discord timestamp format
        /// </summary>
        string ToDiscordTimestamp(DateTime dateTime);

        /// <summary>
        /// Converts a TimeSpan from now to Discord timestamp format
        /// </summary>
        string ToDiscordTimestamp(TimeSpan fromNow);
    }

    /// <summary>
    /// Implementation of embed service
    /// </summary>
    public class EmbedService : IEmbedService
    {
        public DiscordEmbed CreateSimpleEmbed(string title = null, string description = null, DiscordColor? color = null)
        {
            return new DiscordEmbedBuilder
            {
                Title = title,
                Description = description,
                Color = color ?? DiscordColor.CornflowerBlue
            }.Build();
        }

        public DiscordEmbed CreateTrackEmbed(LavalinkTrack track, string status, string requestedBy, int queueCount = 0)
        {
            var (color, iconUrl, imageUrl) = GetEmbedStyling(status);
            
            var trackDuration = track.Duration;

            string length = trackDuration == TimeSpan.Zero ? "LIVE" : trackDuration.ToString(@"mm\:ss");
            string queueText = GetQueueText(status, queueCount);
            string footerText = status == "Playing" ? $"Queued by {requestedBy}\n{queueText}" : queueText;

            var embedBuilder = new DiscordEmbedBuilder
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"[{length}] {track.Author}\n{track.Title}",
                    IconUrl = iconUrl
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = footerText
                },
                ImageUrl = imageUrl,
                Description = track.Uri?.ToString() ?? string.Empty,
                Color = color
            };

            return embedBuilder.Build();
        }

        public DiscordEmbed CreateProgressEmbed(TimeSpan position, TimeSpan duration, int barLength)
        {
            var timeFormat = duration.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss";
            var positionStr = position.ToString(timeFormat);
            var remainingStr = (duration - position).ToString(timeFormat);
            
            var progressBar = GenerateProgressBar(position, duration, barLength);
            var progressText = $"{positionStr} {progressBar} -{remainingStr}";

            return new DiscordEmbedBuilder
            {
                Description = $"```{progressText}```",
                Color = DiscordColor.Purple
            }.Build();
        }

        public string ToDiscordTimestamp(DateTime dateTime)
        {
            long unixSeconds = ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
            return $"<t:{unixSeconds}:R>";
        }

        public string ToDiscordTimestamp(TimeSpan fromNow)
        {
            return ToDiscordTimestamp(DateTime.Now + fromNow);
        }

        private string GenerateProgressBar(TimeSpan current, TimeSpan total, int size)
        {
            if (total.TotalSeconds <= 0)
                return $"[{new string('─', size)}]";

            var progress = (int)Math.Round(size * (current.TotalSeconds / total.TotalSeconds));

            if (progress < 1)
            {
                size--; // Adjust size for the starting point
            }

            var progressText = new string('─', Math.Max(0, progress - 1));
            var emptyProgressText = new string('─', size - progress);

            return $"[{progressText}⊙{emptyProgressText}]";
        }

        private (DiscordColor color, string iconUrl, string imageUrl) GetEmbedStyling(string status)
        {
            return status switch
            {
                "Playing" => (
                    DiscordColor.Purple,
                    "https://m.media-amazon.com/images/G/01/digital/music/player/web/sixteen_frame_equalizer_accent.gif",
                    "https://mir-s3-cdn-cf.behance.net/project_modules/max_1200/a5341856722913.59bb2a94979c8.gif"
                ),
                "Added" => (DiscordColor.DarkGreen, string.Empty, string.Empty),
                "Queued" => (DiscordColor.Orange, string.Empty, string.Empty),
                _ => (DiscordColor.White, string.Empty, string.Empty)
            };
        }

        private string GetQueueText(string status, int queueCount)
        {
            if (queueCount <= 1)
                return string.Empty;

            var prefix = status switch
            {
                "Playing" => "Songs in Queue",
                "Added" or "Queued" => "Queue",
                _ => string.Empty
            };

            return string.IsNullOrEmpty(prefix) ? string.Empty : $"{prefix}: {queueCount - 1}";
        }
    }
}
