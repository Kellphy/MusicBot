using DiscordBot;
using DiscordBot.Commands;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using MusicBot.Models;
using MusicBot.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MusicBot.Events
{
    /// <summary>
    /// Handles Discord client events and interactions
    /// </summary>
    public class DiscordEvents
    {

        /// <summary>
        /// Registers event handlers for Discord client
        /// </summary>
        public void EventsFeedback(DiscordClient client)
        {
            client.ClientErrored += OnClientError;
            client.SocketOpened += OnSocketOpened;
            client.Resumed += OnResumed;
            client.SocketClosed += OnSocketClosed;
            client.GuildUnavailable += OnGuildUnavailable;
            client.GuildDownloadCompleted += OnGuildDownloadCompleted;
            client.ComponentInteractionCreated += OnComponentInteraction;
            client.VoiceStateUpdated += OnVoiceStateUpdated;
        }

        private Task OnClientError(DiscordClient c, ClientErrorEventArgs e)
        {
            GetService<ILoggingService>().LogError($"Client Error: {e.EventName} {e.Exception}");
            return Task.CompletedTask;
        }

        private Task OnSocketOpened(DiscordClient c, SocketEventArgs e)
        {
            GetService<ILoggingService>().LogEvent("WebSocket Open");
            return Task.CompletedTask;
        }

        private Task OnResumed(DiscordClient c, ReadyEventArgs e)
        {
            GetService<ILoggingService>().LogEvent("Resumed");
            return Task.CompletedTask;
        }

        private Task OnSocketClosed(DiscordClient c, SocketCloseEventArgs e)
        {
            GetService<ILoggingService>().LogEvent($"WebSocket Closed: {e.CloseCode} {e.CloseMessage}");
            return Task.CompletedTask;
        }

        private Task OnGuildUnavailable(DiscordClient c, GuildDeleteEventArgs e)
        {
            GetService<ILoggingService>().LogError($"Guild Unavailable: {e.Guild.Name} ({e.Guild.Id})");
            return Task.CompletedTask;
        }

        private Task OnGuildDownloadCompleted(DiscordClient c, GuildDownloadCompletedEventArgs e)
        {
            GetService<ILoggingService>().LogEvent("GuildDownloadCompleted");
            _ = Task.Run(() => GuildDownloadCompletedAsync(c, e));
            return Task.CompletedTask;
        }

        private Task OnComponentInteraction(DiscordClient c, ComponentInteractionCreateEventArgs e)
        {
            var logging = GetService<ILoggingService>();
            logging.LogInfo($"Button: {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) | " +
                $"Guild: {e.Guild.Name} ({e.Guild.Id}) | Channel: {e.Channel.Name} ({e.Channel.Id})");
            _ = Task.Run(() => InteractionCreatedAsync(c, e));
            return Task.CompletedTask;
        }

        private Task OnVoiceStateUpdated(DiscordClient c, VoiceStateUpdateEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                var clientMember = await e.Guild.GetMemberAsync(c.CurrentUser.Id);
                if (clientMember?.VoiceState?.Channel != null &&
                    clientMember.VoiceState.Channel.Users.Count < 2)
                {
                    var logging = GetService<ILoggingService>();
                    var playerManager = GetService<IPlayerManagerService>();
                    
                    logging.LogInfo("No more users in the voice channel, disconnecting");
                    await playerManager.DisposePlayerAsync();
                }
            });
            return Task.CompletedTask;
        }

        private async Task GuildDownloadCompletedAsync(DiscordClient client, GuildDownloadCompletedEventArgs e)
        {
            await UpdateStatusAsync(client, "Connecting ...");
            await UpdateStatusAsync(client);
        }

        private async Task UpdateStatusAsync(DiscordClient client, string newStatus = "")
        {
            DiscordActivity discordActivity;
            if (!string.IsNullOrEmpty(newStatus))
            {
                discordActivity = new DiscordActivity(newStatus, ActivityType.Playing);
            }
            else
            {
                discordActivity = new DiscordActivity(
                    $"with \"/play\" | v.{BotConstants.Version}", ActivityType.Playing);
            }
            await client.UpdateStatusAsync(discordActivity);
        }

        private async Task InteractionCreatedAsync(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            try
            {
                var services = Bot.GetServiceProvider();
                var logging = services.GetRequiredService<ILoggingService>();
                var embedService = services.GetRequiredService<IEmbedService>();
                var shortcutService = services.GetRequiredService<IShortcutService>();

                string id = e.Values.Length > 0
                    ? e.Values.FirstOrDefault() // Select Menu
                    : e.Id; // Button

                logging.LogInfo($"Interaction ID: {id}");

                if (!id.StartsWith("kb_voice_"))
                    return;

                id = id.Substring(9); // Remove "kb_voice_" prefix

                switch (id)
                {
                    case "skip":
                    case "skip5":
                    case "skip10":
                    case "skip50":
                        int skipCount = id switch
                        {
                            "skip5" => 5,
                            "skip10" => 10,
                            "skip50" => 50,
                            _ => 1
                        };
                        await HandleVoiceActionButtonAsync(e, client, VoiceAction.Skip, skipCount);
                        break;

                    case "pause":
                        await HandleVoiceActionButtonAsync(e, client, VoiceAction.Pause);
                        break;

                    case "resume":
                        await HandleVoiceActionButtonAsync(e, client, VoiceAction.Resume);
                        break;

                    case "stop":
                        await HandleVoiceActionButtonAsync(e, client, VoiceAction.Stop);
                        break;

                    case "queue":
                        var voiceCommandsForQueue = GetService<VoiceSlashCommands>();
                        var queueEmbed = voiceCommandsForQueue != null 
                            ? voiceCommandsForQueue.GetQueueEmbedPublic() 
                            : embedService.CreateSimpleEmbed("Queue", "Service unavailable");
                        await e.Interaction.CreateResponseAsync(
                            InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().AddEmbed(queueEmbed).AsEphemeral());
                        break;

                    case "shortcuts":
                        var shortcutsDisplay = shortcutService.GetShortcutsDisplay();
                        await e.Interaction.CreateResponseAsync(
                            InteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder()
                                .WithContent(shortcutsDisplay)
                                .AsEphemeral());
                        break;

                    case "seek_forward":
                        await HandleSeekButtonAsync(e, client, $"+{BotConstants.QuickSeekSeconds}");
                        break;

                    case "seek_backward":
                        await HandleSeekButtonAsync(e, client, $"-{BotConstants.QuickSeekSeconds}");
                        break;
                }
            }
            catch (Exception ex)
            {
                GetService<ILoggingService>().LogError($"Interaction Error: {ex}");
            }
        }

        /// <summary>
        /// Sends an ephemeral response and deletes it after a delay
        /// </summary>
        private async Task SendEphemeralResponseAsync(
            ComponentInteractionCreateEventArgs e,
            string message)
        {
            await e.Interaction.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent(message)
                    .AsEphemeral());

            await Task.Delay(TimeSpan.FromSeconds(BotConstants.MessageDeleteSeconds));
            await e.Interaction.DeleteOriginalResponseAsync();
        }

        private async Task HandleVoiceActionButtonAsync(
            ComponentInteractionCreateEventArgs e,
            DiscordClient client,
            VoiceAction action,
            int skips = 1)
        {
            var voiceCommands = GetService<VoiceSlashCommands>();
            if (voiceCommands == null)
            {
                await SendEphemeralResponseAsync(e, "Voice commands service not available");
                return;
            }

            await voiceCommands.HandleVoiceActionFromButton(e, client, action, skips);
        }

        private async Task HandleSeekButtonAsync(
            ComponentInteractionCreateEventArgs e,
            DiscordClient client,
            string timeString)
        {
            var voiceCommands = GetService<VoiceSlashCommands>();
            if (voiceCommands == null)
            {
                await SendEphemeralResponseAsync(e, "Voice commands service not available");
                return;
            }

            await voiceCommands.HandleSeekFromButton(e, client, timeString);
        }

        /// <summary>
        /// Generic method to get any service from the service provider
        /// </summary>
        private T GetService<T>(DiscordClient client = null) where T : class
        {
            return Bot.GetServiceProvider().GetRequiredService<T>();
        }
    }
}
