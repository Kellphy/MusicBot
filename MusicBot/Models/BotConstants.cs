namespace MusicBot.Models
{
    /// <summary>
    /// Contains constant values used throughout the application
    /// </summary>
    public static class BotConstants
    {
        /// <summary>
        /// Current version of the bot
        /// </summary>
        public const string Version = "4.0";

        /// <summary>
        /// Seconds to wait before auto-deleting command response messages
        /// </summary>
        public const int MessageDeleteSeconds = 10;

        /// <summary>
        /// Progress bar update interval in seconds
        /// </summary>
        public const int ProgressUpdateIntervalSeconds = 5;

        /// <summary>
        /// Maximum number of tracks to display in queue
        /// </summary>
        public const int MaxQueueDisplayCount = 10;

        /// <summary>
        /// Maximum tracks to accept in a single play command
        /// </summary>
        public const int MaxTracksPerCommand = 100;

        /// <summary>
        /// Default progress bar length in characters
        /// </summary>
        public const int DefaultProgressBarLength = 45;

        /// <summary>
        /// Seek forward/backward duration in seconds for quick buttons
        /// </summary>
        public const int QuickSeekSeconds = 10;

        /// <summary>
        /// Links text file name
        /// </summary>
        public const string LinksFileName = "links.txt";

        /// <summary>
        /// ASCII art banner for Kellphy branding
        /// </summary>
        public const string KellphyBanner = @"

   ▄█   ▄█▄    ▄████████  ▄█        ▄█          ▄███████▄    ▄█    █▄    ▄██   ▄
  ███ ▄███▀   ███    ███ ███       ███         ███    ███   ███    ███   ███   ██▄
  ███▐██▀     ███    █▀  ███       ███         ███    ███   ███    ███   ███▄▄▄███
 ▄█████▀     ▄███▄▄▄     ███       ███         ███    ███  ▄███▄▄▄▄███▄▄ ▀▀▀▀▀▀███
▀▀█████▄    ▀▀███▀▀▀     ███       ███       ▀█████████▀  ▀▀███▀▀▀▀███▀  ▄██   ███
  ███▐██▄     ███    █▄  ███       ███         ███          ███    ███   ███   ███
  ███ ▀███▄   ███    ███ ███▌    ▄ ███▌    ▄   ███          ███    ███   ███   ███
  ███   ▀█▀   ██████████ █████▄▄██ █████▄▄██  ▄████▀        ███    █▀     ▀█████▀

";

        /// <summary>
        /// Embed links for bot information
        /// </summary>
        public const string EmbedLinks =
            "[Discord](https://kellphy.com/discord)" +
            " | [Website](https://kellphy.com)" +
            " | [Patreon](https://www.kellphy.com/patreon)" +
            " | [Get Bot](https://kellphy.github.io/MusicBot)";

        /// <summary>
        /// Visual separator for embeds
        /// </summary>
        public const string EmbedBreak = "▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬";
    }
}
