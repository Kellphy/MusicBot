using KellphyBot;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace DiscordBot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //Suppressed IDE0008 / IDE1006
            //var bot = new Bot();
            //Runs bot async, let's commands work after this line
            //bot.RunAsync().Result;

            CreateWebHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateWebHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
    }
}
