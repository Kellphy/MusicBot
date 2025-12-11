using DSharpPlus;
using DSharpPlus.Entities;

namespace MusicBot.Services
{
    /// <summary>
    /// Service for creating Discord message buttons and interactions
    /// </summary>
    public interface IButtonService
    {
        /// <summary>
        /// Creates a message builder with music control buttons
        /// </summary>
        DiscordMessageBuilder CreateMessageWithButtons(DiscordEmbed mainEmbed, DiscordEmbed progressEmbed = null);
    }

    /// <summary>
    /// Implementation of button service
    /// </summary>
    public class ButtonService : IButtonService
    {
        public DiscordMessageBuilder CreateMessageWithButtons(DiscordEmbed mainEmbed, DiscordEmbed progressEmbed = null)
        {
            var builder = new DiscordMessageBuilder();
            builder.AddEmbed(mainEmbed);

            if (progressEmbed != null)
            {
                builder.AddEmbed(progressEmbed);
            }

            // First row of buttons
            builder.AddComponents(new DiscordComponent[]
            {
                new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_queue", "Show Queue"),
                new DiscordButtonComponent(ButtonStyle.Primary, "kb_voice_pause", "Pause"),
                new DiscordButtonComponent(ButtonStyle.Success, "kb_voice_resume", "Resume"),
                new DiscordButtonComponent(ButtonStyle.Danger, "kb_voice_stop", "Disconnect")
            });

            // Second row of buttons
            builder.AddComponents(new DiscordComponent[]
            {
                new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_shortcuts", "Shortcuts"),
                new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_skip", "Skip"),
				new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_seek_backward", "< 10s"),
				new DiscordButtonComponent(ButtonStyle.Secondary, "kb_voice_seek_forward", "10s >")
            });

            return builder;
        }
    }
}
