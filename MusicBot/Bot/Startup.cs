using DiscordBot;
using DSharpPlus;
using Lavalink4NET;
using Lavalink4NET.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MusicBot
{
    public class BotHostedService : IHostedService
    {
        private readonly Bot _bot;
        private readonly IAudioService _audioService;

        public BotHostedService(Bot bot, IAudioService audioService)
        {
            _bot = bot;
            _audioService = audioService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Start Lavalink audio service first
            await _audioService.StartAsync(cancellationToken);
            DataMethods.SendLogs("Lavalink AudioService started and connected!");
            
            // Bot is already initialized in constructor
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _audioService.StopAsync(cancellationToken);
        }
    }

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // Load config first to get token
            var json = string.Empty;
            if (!File.Exists("config.json"))
            {
                DataMethods.SendErrorLogs("config.json is missing from your directory");
                return;
            }

            using (var fs = File.OpenRead("config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = sr.ReadToEnd();
            
            var configJson = JsonConvert.DeserializeObject<ConfigJson>(json);
            CustomAttributes.configJson = configJson;

            // Create and register DiscordClient
            var discordConfig = new DiscordConfiguration
            {
                Token = configJson.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Error,
                Intents = DiscordIntents.AllUnprivileged,
            };

            var discordClient = new DiscordClient(discordConfig);
            services.AddSingleton(discordClient);

            // Configure Lavalink based on OS
            string endpointHost;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                endpointHost = "127.0.0.1";
            }
            else
            {
                endpointHost = "c-lvl";
            }

            // Add Lavalink4NET services (after DiscordClient is registered)
            services.AddLavalink();
            services.ConfigureLavalink(config =>
            {
                config.BaseAddress = new Uri($"http://{endpointHost}:2333");
                config.Passphrase = "youshallnotpass";
                config.ReadyTimeout = TimeSpan.FromSeconds(30);
            });

            // Register Bot as singleton
            services.AddSingleton<Bot>();
            
            // Add hosted service to initialize bot
            services.AddHostedService<BotHostedService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) { }
    }
}