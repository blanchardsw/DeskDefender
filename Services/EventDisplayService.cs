using System;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Service responsible for displaying event summaries to the user
    /// </summary>
    public class EventDisplayService
    {
        private readonly ILogger<EventDisplayService> _logger;

        public EventDisplayService(ILogger<EventDisplayService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Displays an event summary to the user
        /// </summary>
        /// <param name="summary">The event summary to display</param>
        public void DisplaySummary(EventSummary summary)
        {
            try
            {
                // Display to console for now (can be extended to UI later)
                var summaryText = summary.GetSummaryDescription();
                
                // Use different colors for different types of activity
                if (summary.KeyboardActivity?.HasActivity == true && summary.MouseActivity?.HasActivity == true)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan; // Both keyboard and mouse
                }
                else if (summary.KeyboardActivity?.HasActivity == true)
                {
                    Console.ForegroundColor = ConsoleColor.Green; // Keyboard only
                }
                else if (summary.MouseActivity?.HasActivity == true)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow; // Mouse only
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Gray; // No activity
                }

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {summaryText}");
                Console.ResetColor();

                // Log detailed information for debugging
                _logger.LogInformation("Event summary displayed: {Summary}", summaryText);
                
                if (summary.KeyboardActivity?.HasActivity == true)
                {
                    _logger.LogDebug("Keyboard details - Keystrokes: {Count}, Speed: {Speed:F1} CPM, Text: '{Text}'", 
                        summary.KeyboardActivity.KeystrokeCount, 
                        summary.KeyboardActivity.TypingSpeed,
                        summary.KeyboardActivity.TextTyped);
                }

                if (summary.MouseActivity?.HasActivity == true)
                {
                    _logger.LogDebug("Mouse details - Clicks: {Clicks}, Movements: {Movements}, Distance: {Distance:F1}px, Active time: {Time}s", 
                        summary.MouseActivity.ClickCount,
                        summary.MouseActivity.MovementEvents,
                        summary.MouseActivity.TotalDistance,
                        summary.MouseActivity.ActiveMovementTime.TotalSeconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error displaying event summary");
            }
        }

        /// <summary>
        /// Displays configuration information
        /// </summary>
        /// <param name="intervalSeconds">Current summary interval in seconds</param>
        public void DisplayConfiguration(double intervalSeconds)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"[CONFIG] Event summary interval: {intervalSeconds}s");
            Console.ResetColor();
            
            _logger.LogInformation("Configuration displayed - Interval: {Interval}s", intervalSeconds);
        }

        /// <summary>
        /// Displays startup information
        /// </summary>
        public void DisplayStartupInfo()
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("=== DeskDefender Event Monitoring Started ===");
            Console.WriteLine("Events will be summarized and displayed in batches.");
            Console.WriteLine("Colors: Green=Keyboard, Yellow=Mouse, Cyan=Both, Gray=No activity");
            Console.WriteLine("===============================================");
            Console.ResetColor();
            
            _logger.LogInformation("Event monitoring startup info displayed");
        }
    }
}
