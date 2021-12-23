using DSharpPlus.Entities;
using System;


namespace DiscordBot
{
    public sealed class DataMethods
    {
        public static DiscordEmbedBuilder SimpleEmbed(string title = null, string description = null)
        {
            var sEmbed = new DiscordEmbedBuilder
            {
                Title = title,
                Description = description,
                Color = DiscordColor.CornflowerBlue
            };
            return sEmbed;
        }

        public static void SendLogs(MyContext ctx)
        {
            Console.Write($"\u001b[36m{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")} [UTC] - ");
            Console.Write($"\u001b[36m{ctx.User.Username}#{ctx.User.Discriminator} ({ctx.User.Id}) ");
            Console.Write($"\u001b[31m{ctx.CommandName} ");
            Console.Write($"\u001b[32m{ctx.Guild.Name} ({ctx.Guild.Id}) ");
            Console.Write($"\u001b[35m{ctx.Channel.Name} ({ctx.Channel.Id}) ");
            Console.Write($"\u001b[0m\n");
        }

        public static void SendLogs(string eventName, string eventDetails = "")
        {
            eventName = eventName.Replace("EventArgs", "");
            if (eventDetails.Length > 0)
            {
                eventDetails = " - " + eventDetails;
            }
            Console.WriteLine($"\u001b[36m{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")} [UTC] - \u001b[33m{eventName}\u001b[0m" + eventDetails);
        }
        public static void SendErrorLogs(string text)
        {
            Console.WriteLine($"\u001b[36m{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")} [UTC] - \u001b[91m{text}\u001b[0m");
        }
        public static void SendKellphy()
        {
            Console.WriteLine($"\u001b[36m{CustomStrings.kellphy}\u001b[0m");
        }
    }
}