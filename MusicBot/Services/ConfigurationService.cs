using MusicBot.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace MusicBot.Services
{
    /// <summary>
    /// Service for managing bot configuration
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// Gets the loaded bot configuration
        /// </summary>
        BotConfiguration Configuration { get; }

        /// <summary>
        /// Loads configuration from config.json file
        /// </summary>
        bool LoadConfiguration();
    }

    /// <summary>
    /// Implementation of configuration service
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private readonly ILoggingService _loggingService;
        private const string ConfigFileName = "config.json";

        public BotConfiguration Configuration { get; private set; }

        public ConfigurationService(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public bool LoadConfiguration()
        {
            try
            {
                if (!File.Exists(ConfigFileName))
                {
                    _loggingService.LogError($"{ConfigFileName} is missing from your directory");
                    return false;
                }

                using var fs = File.OpenRead(ConfigFileName);
                using var sr = new StreamReader(fs, new UTF8Encoding(false));
                var json = sr.ReadToEnd();
                
                Configuration = JsonConvert.DeserializeObject<BotConfiguration>(json);
                
                if (Configuration == null || string.IsNullOrWhiteSpace(Configuration.Token))
                {
                    _loggingService.LogError("Invalid configuration: Token is required");
                    return false;
                }

                _loggingService.LogInfo("Configuration loaded successfully");
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to load configuration: {ex.Message}");
                return false;
            }
        }
    }
}
