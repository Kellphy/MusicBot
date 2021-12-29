using DiscordBot;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace KellphyBot
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(new Bot(services.BuildServiceProvider()));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) { }
    }
}
