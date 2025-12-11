using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using MusicBot.Services;
using System.Threading.Tasks;

namespace MusicBot.Extensions
{
    /// <summary>
    /// Extension methods for Discord interaction builders
    /// </summary>
    public static class InteractionExtensions
    {
        /// <summary>
        /// Sets content and logs the message
        /// </summary>
        public static DiscordInteractionResponseBuilder WithLoggedContent(
            this DiscordInteractionResponseBuilder builder,
            string content,
            ILoggingService loggingService)
        {
            loggingService?.LogInfo(content);
            return builder.WithContent(content);
        }

        /// <summary>
        /// Sets content and logs the message for webhook builder
        /// </summary>
        public static DiscordWebhookBuilder WithLoggedContent(
            this DiscordWebhookBuilder builder,
            string content,
            ILoggingService loggingService)
        {
            loggingService?.LogInfo(content);
            return builder.WithContent(content);
        }

        /// <summary>
        /// Logs a message without modifying builder (for InteractionResponseBuilder)
        /// </summary>
        public static DiscordInteractionResponseBuilder WithLog(
            this DiscordInteractionResponseBuilder builder,
            string content,
            ILoggingService loggingService)
        {
            loggingService?.LogInfo(content);
            return builder;
        }

        /// <summary>
        /// Logs a message without modifying builder (for WebhookBuilder)
        /// </summary>
        public static DiscordWebhookBuilder WithLog(
            this DiscordWebhookBuilder builder,
            string content,
            ILoggingService loggingService)
        {
            loggingService?.LogInfo(content);
            return builder;
        }

        /// <summary>
        /// Delays and deletes a response message
        /// </summary>
        public static async Task DeleteAfterDelayAsync(this InteractionContext context, int seconds)
        {
            await Task.Delay(System.TimeSpan.FromSeconds(seconds));
            await context.DeleteResponseAsync();
        }
    }
}
