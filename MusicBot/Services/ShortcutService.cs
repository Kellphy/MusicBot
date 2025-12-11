using MusicBot.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MusicBot.Services
{
    /// <summary>
    /// Service for managing music shortcuts from links.txt
    /// </summary>
    public interface IShortcutService
    {
        /// <summary>
        /// Gets all loaded shortcuts
        /// </summary>
        IReadOnlyList<MusicShortcut> Shortcuts { get; }

        /// <summary>
        /// Loads shortcuts from links.txt file
        /// </summary>
        void LoadShortcuts();

        /// <summary>
        /// Resolves a shortcut name to its full link, or returns the input if not a shortcut
        /// </summary>
        string ResolveShortcut(string input);

        /// <summary>
        /// Gets a comma-separated string of all shortcut names
        /// </summary>
        string GetShortcutsDisplay();
    }

    /// <summary>
    /// Implementation of shortcut service
    /// </summary>
    public class ShortcutService : IShortcutService
    {
        private readonly ILoggingService _loggingService;
        private readonly List<MusicShortcut> _shortcuts = new();

        public IReadOnlyList<MusicShortcut> Shortcuts => _shortcuts.AsReadOnly();

        public ShortcutService(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public void LoadShortcuts()
        {
            _shortcuts.Clear();

            if (!File.Exists(BotConstants.LinksFileName))
            {
                _loggingService.LogInfo($"{BotConstants.LinksFileName} not found. Shortcuts will not be available.");
                return;
            }

            try
            {
                var lines = File.ReadAllLines(BotConstants.LinksFileName);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        _shortcuts.Add(new MusicShortcut
                        {
                            Name = parts[0],
                            Link = parts[1]
                        });
                    }
                }

                _loggingService.LogInfo($"Loaded {_shortcuts.Count} shortcuts from {BotConstants.LinksFileName}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to load shortcuts: {ex.Message}");
            }
        }

        public string ResolveShortcut(string input)
        {
            var shortcut = _shortcuts.FirstOrDefault(s => s.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
            return shortcut?.Link ?? input;
        }

        public string GetShortcutsDisplay()
        {
            return _shortcuts.Any()
                ? string.Join(", ", _shortcuts.Select(s => s.Name))
                : "Check out [this](https://kellphy.com/musicbot) guide to add shortcut links.";
        }
    }
}
