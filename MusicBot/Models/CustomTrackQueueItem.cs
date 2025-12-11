using System.Collections.Concurrent;

namespace MusicBot.Models
{
    /// <summary>
    /// Static tracker for track requesters
    /// </summary>
    public static class TrackRequesterTracker
    {
        private static readonly ConcurrentDictionary<string, string> _requesters = new();

        public static void SetRequester(string trackIdentifier, string requesterUsername)
        {
            _requesters[trackIdentifier] = requesterUsername;
        }

        public static string GetRequester(string trackIdentifier)
        {
            return _requesters.TryGetValue(trackIdentifier, out var requester) ? requester : "Unknown";
        }

        public static void Clear()
        {
            _requesters.Clear();
        }
    }
}
