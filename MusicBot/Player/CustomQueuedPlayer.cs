using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Player
{
    public class CustomQueuedPlayer : QueuedLavalinkPlayer, IDisposable
    {
        private bool _disposed;
        
        public event Func<ITrackQueueItem, Task> TrackStarted;
        public event Func<ITrackQueueItem, TrackEndReason, Task> TrackEnded;

        public CustomQueuedPlayer(IPlayerProperties<CustomQueuedPlayer, CustomQueuedPlayerOptions> properties)
            : base(properties)
        {
        }

        protected override async ValueTask NotifyTrackStartedAsync(ITrackQueueItem trackQueueItem, CancellationToken cancellationToken = default)
        {
            await base.NotifyTrackStartedAsync(trackQueueItem, cancellationToken);
            
            if (TrackStarted != null)
            {
                await TrackStarted.Invoke(trackQueueItem);
            }
        }

        protected override async ValueTask NotifyTrackEndedAsync(ITrackQueueItem trackQueueItem, TrackEndReason endReason, CancellationToken cancellationToken = default)
        {
            await base.NotifyTrackEndedAsync(trackQueueItem, endReason, cancellationToken);
            
            if (TrackEnded != null)
            {
                await TrackEnded.Invoke(trackQueueItem, endReason);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            TrackStarted = null;
            TrackEnded = null;
        }
    }

    public record CustomQueuedPlayerOptions : QueuedLavalinkPlayerOptions
    {
    }
}
