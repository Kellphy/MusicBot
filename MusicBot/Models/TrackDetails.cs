using DSharpPlus.Entities;
using System;

namespace MusicBot.Models
{
    /// <summary>
    /// Represents details about a queued track
    /// </summary>
    public class TrackDetails
    {
        /// <summary>
        /// The Discord member who requested this track
        /// </summary>
        public DiscordMember Member { get; set; }

        /// <summary>
        /// The URL or URI of the track
        /// </summary>
        public string Link { get; set; }

        /// <summary>
        /// The display title of the track
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The duration of the track (TimeSpan.Zero for live streams)
        /// </summary>
        public TimeSpan Length { get; set; }
    }
}
