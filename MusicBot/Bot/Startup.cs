using DiscordBot;
using DiscordBot.Commands;
using DSharpPlus;
using Lavalink4NET;
using Lavalink4NET.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MusicBot.Models;
using MusicBot.Services;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MusicBot
{
    /// <summary>
    /// Hosted service that initializes the bot and audio services
    /// </summary>
    public class BotHostedService : IHostedService
    {
        private readonly Bot _bot;
        private readonly IAudioService _audioService;
        private readonly ILoggingService _loggingService;

        public BotHostedService(Bot bot, IAudioService audioService, ILoggingService loggingService)
        {
            _bot = bot;
            _audioService = audioService;
            _loggingService = loggingService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Start Lavalink audio service first
            await _audioService.StartAsync(cancellationToken);
            _loggingService.LogInfo("Lavalink AudioService started and connected!");
            
            // Bot is already initialized in constructor
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _audioService.StopAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Application startup configuration
    /// </summary>
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            RegisterCoreServices(services);
            var config = LoadConfiguration(services);
            RegisterApplicationServices(services);
            RegisterDiscordClient(services, config);
            RegisterLavalink(services);
            
            services.AddSingleton<Bot>();
            services.AddHostedService<BotHostedService>();
        }

        private void RegisterCoreServices(IServiceCollection services)
        {
            services.AddSingleton<ILoggingService, LoggingService>();
            
            // Create and register pre-loaded configuration service
            var loggingService = new LoggingService();
            var configService = new ConfigurationService(loggingService);
            
            if (!configService.LoadConfiguration())
            {
                throw new InvalidOperationException("Failed to load configuration");
            }
            
            services.AddSingleton<IConfigurationService>(configService);
        }

        private BotConfiguration LoadConfiguration(IServiceCollection services)
        {
            var serviceProvider = services.BuildServiceProvider();
            var configService = serviceProvider.GetRequiredService<IConfigurationService>();
            return configService.Configuration;
        }

        private void RegisterApplicationServices(IServiceCollection services)
        {
            services.AddSingleton<IShortcutService, ShortcutService>();
            services.AddSingleton<IEmbedService, EmbedService>();
            services.AddSingleton<IButtonService, ButtonService>();
            services.AddSingleton<IMusicService, MusicService>();
            services.AddSingleton<IPlayerManagerService, PlayerManagerService>();
            services.AddSingleton<IProgressTrackerService, ProgressTrackerService>();
            services.AddSingleton<VoiceSlashCommands>();
        }

        private void RegisterDiscordClient(IServiceCollection services, BotConfiguration config)
        {
            var discordConfig = new DiscordConfiguration
            {
                Token = config.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Error,
                Intents = DiscordIntents.AllUnprivileged,
            };

            services.AddSingleton(new DiscordClient(discordConfig));
        }

        private void RegisterLavalink(IServiceCollection services)
        {
            var endpointHost = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? "127.0.0.1" 
                : "c-lvl";

            services.AddLavalink();
            services.ConfigureLavalink(config =>
            {
                config.BaseAddress = new Uri($"http://{endpointHost}:2333");
                config.Passphrase = "youshallnotpass";
                config.ReadyTimeout = TimeSpan.FromSeconds(30);
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) { }
    }
}