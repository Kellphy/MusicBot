using Lavalink4NET;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using MusicBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MusicBot.Services
{
    /// <summary>
    /// Service for loading and playing music tracks
    /// </summary>
    public interface IMusicService
    {
        /// <summary>
        /// Searches and loads tracks from a search query or URL
        /// </summary>
        Task<(bool success, List<LavalinkTrack> tracks, string errorMessage)> LoadTracksAsync(string searchQuery);

        /// <summary>
        /// Converts a Spotify track URL to a search query
        /// </summary>
        Task<string> ConvertSpotifyUrlAsync(string url);

        /// <summary>
        /// Parses a time string (mm:ss, hh:mm:ss, +mm:ss, -mm:ss, or seconds)
        /// </summary>
        bool TryParseTimeString(string timeString, out TimeSpan result);
    }

    /// <summary>
    /// Implementation of music service
    /// </summary>
    public class MusicService : IMusicService
    {
        private readonly IAudioService _audioService;
        private readonly ILoggingService _loggingService;
        private static readonly HttpClient _httpClient = new();

        public MusicService(IAudioService audioService, ILoggingService loggingService)
        {
            _audioService = audioService;
            _loggingService = loggingService;
        }

        public async Task<(bool success, List<LavalinkTrack> tracks, string errorMessage)> LoadTracksAsync(string searchQuery)
        {
            try
            {
                TrackLoadResult loadResult;

                // If it's a URL, try loading directly
                if (searchQuery.StartsWith("http"))
                {
                    loadResult = await _audioService.Tracks.LoadTracksAsync(searchQuery, TrackSearchMode.None);
                }
                else
                {
                    // Try YouTube search first
                    loadResult = await _audioService.Tracks.LoadTracksAsync(searchQuery, TrackSearchMode.YouTube);
                }

                if (!loadResult.IsSuccess || !loadResult.Tracks.Any())
                {
                    // Try SoundCloud if YouTube fails
                    loadResult = await _audioService.Tracks.LoadTracksAsync(searchQuery, TrackSearchMode.SoundCloud);
                }

                if (!loadResult.IsSuccess || !loadResult.Tracks.Any())
                {
                    return (false, null, $"Track search failed for: {searchQuery}");
                }

                var tracks = loadResult.Tracks.Take(BotConstants.MaxTracksPerCommand).ToList();

                // For non-URL searches, take only the first result
                if (!searchQuery.StartsWith("http") && tracks.Any())
                {
                    tracks = new List<LavalinkTrack> { tracks[0] };
                }

                _loggingService.LogInfo($"Loaded {tracks.Count} track(s) for query: {searchQuery}");
                return (true, tracks, string.Empty);
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to load tracks: {ex.Message}");
                return (false, null, $"An error occurred while loading tracks: {ex.Message}");
            }
        }

        public async Task<string> ConvertSpotifyUrlAsync(string url)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.102 Safari/537.36");

                var responseString = await _httpClient.GetStringAsync(url);

                var regex = new Regex(
                    @"<meta property=\""og:title\"" content=\""(?<title>.*?)\""/>.*<meta property=\""og:description\"" content=\""(?<description>.*?)\""/>",
                    RegexOptions.Singleline);

                var match = regex.Match(responseString);

                if (match.Success)
                {
                    var title = match.Groups["title"].Value;
                    var description = match.Groups["description"].Value;
                    return $"{title} {description}";
                }

                _loggingService.LogInfo("Failed to extract Spotify track info, using original URL");
                return url;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to convert Spotify URL: {ex.Message}");
                return url;
            }
        }

        public bool TryParseTimeString(string timeString, out TimeSpan result)
        {
            result = TimeSpan.Zero;

            // Try parsing as TimeSpan format (supports h:mm:ss, hh:mm:ss, m:ss, mm:ss, ss, s)
            if (TimeSpan.TryParseExact(timeString, new[]
            {
                @"h\:mm\:ss", @"hh\:mm\:ss", @"h\:m\:ss", @"h\:mm\:s", @"h\:m\:s",
                @"m\:ss", @"mm\:ss", @"m\:s",
                @"ss", @"s"
            }, null, out result))
            {
                return true;
            }

            // Try parsing as total seconds
            if (int.TryParse(timeString, out int seconds))
            {
                result = TimeSpan.FromSeconds(seconds);
                return true;
            }

            return false;
        }
    }
}
