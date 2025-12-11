using System;

namespace MusicBot.Services
{
    /// <summary>
    /// Service for logging bot activities and errors to console
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// Log a general information message
        /// </summary>
        void LogInfo(string message);

        /// <summary>
        /// Log an error message
        /// </summary>
        void LogError(string message);

        /// <summary>
        /// Log an event occurrence
        /// </summary>
        void LogEvent(string eventName, string eventDetails = "");

        /// <summary>
        /// Display the Kellphy banner
        /// </summary>
        void ShowBanner();
    }

    /// <summary>
    /// Implementation of logging service with colored console output
    /// </summary>
    public class LoggingService : ILoggingService
    {
        public void LogInfo(string message)
        {
            Console.WriteLine($"\u001b[36m{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - \u001b[33m{message}\u001b[0m");
        }

        public void LogError(string message)
        {
            Console.WriteLine($"\u001b[36m{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - \u001b[91m{message}\u001b[0m");
        }

        public void LogEvent(string eventName, string eventDetails = "")
        {
            eventName = eventName.Replace("EventArgs", "");
            if (!string.IsNullOrEmpty(eventDetails))
            {
                eventDetails = " - " + eventDetails;
            }
            Console.WriteLine($"\u001b[36m{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - \u001b[33m{eventName}\u001b[0m{eventDetails}");
        }

        public void ShowBanner()
        {
            Console.Clear();
            Console.WriteLine($"\u001b[36m{Models.BotConstants.KellphyBanner}\u001b[0m");
        }
    }
}
