namespace MusicBot.Models
{
    /// <summary>
    /// Represents a music shortcut loaded from links.txt
    /// </summary>
    public class MusicShortcut
    {
        /// <summary>
        /// Short name/alias for the link
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Full URL or search query
        /// </summary>
        public string Link { get; set; }
    }
}
