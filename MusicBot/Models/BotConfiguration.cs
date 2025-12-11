using Newtonsoft.Json;

namespace MusicBot.Models
{
    /// <summary>
    /// Represents the bot configuration loaded from config.json
    /// </summary>
    public class BotConfiguration
    {
        /// <summary>
        /// Discord bot token for authentication
        /// </summary>
        [JsonProperty("token")]
        public string Token { get; set; }

        /// <summary>
        /// Command prefix for text-based commands (legacy support)
        /// </summary>
        [JsonProperty("prefix")]
        public string Prefix { get; set; }

        /// <summary>
        /// If true, only the bot owner can start the music player
        /// </summary>
        [JsonProperty("isOwnerStarted")]
        public bool IsOwnerStarted { get; set; }
    }
}
