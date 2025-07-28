using System;
using System.Threading.Tasks;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;
using DeskDefender.Interfaces;

namespace DeskDefender.Services
{
    /// <summary>
    /// Service responsible for displaying event summaries to the user
    /// </summary>
    public class EventDisplayService
    {
        private readonly ILogger<EventDisplayService> _logger;
        private readonly IEventLogger _eventLogger;

        public EventDisplayService(ILogger<EventDisplayService> logger, IEventLogger eventLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
        }

        /// <summary>
        /// Event fired when a summary should be displayed in the UI
        /// </summary>
        public event Action<EventSummary> SummaryForUI;

        /// <summary>
        /// Displays an event summary to the user
        /// </summary>
        /// <param name="summary">The event summary to display</param>
        public async Task DisplaySummary(EventSummary summary)
        {
            try
            {
                // Fire event for UI to receive the summary
                SummaryForUI?.Invoke(summary);
                
                // Save summary to database for persistence
                await SaveSummaryToDatabase(summary);
                
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

        /// <summary>
        /// Save event summary to database for persistence between sessions
        /// </summary>
        /// <param name="summary">The event summary to save</param>
        private async Task SaveSummaryToDatabase(EventSummary summary)
        {
            try
            {
                // Convert EventSummary to EventLog for database storage
                var eventLog = new EventLog
                {
                    EventType = "EventSummary",
                    Description = summary.GetSummaryDescription(),
                    Timestamp = summary.IntervalStart,
                    Severity = DetermineSeverity(summary),
                    IsAlert = false,
                    Details = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        IntervalStart = summary.IntervalStart,
                        IntervalEnd = summary.IntervalEnd,
                        KeyboardActivity = summary.KeyboardActivity,
                        MouseActivity = summary.MouseActivity
                    })
                };

                // Save to database asynchronously - CRITICAL FIX: Use proper async/await instead of fire-and-forget
                try
                {
                    await _eventLogger.LogEventAsync(eventLog);
                    _logger.LogDebug("Event summary saved to database: {Description}", eventLog.Description);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save event summary to database: {Message}", ex.Message);
                    // Don't rethrow - we want to continue processing even if DB save fails
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing event summary for database storage");
            }
        }

        /// <summary>
        /// Determine severity level for event summary
        /// </summary>
        /// <param name="summary">The event summary</param>
        /// <returns>Appropriate severity level</returns>
        private EventSeverity DetermineSeverity(EventSummary summary)
        {
            // Determine severity based on activity level
            var hasKeyboard = summary.KeyboardActivity?.HasActivity == true;
            var hasMouse = summary.MouseActivity?.HasActivity == true;
            
            if (hasKeyboard && hasMouse)
            {
                return EventSeverity.Medium; // High activity
            }
            else if (hasKeyboard || hasMouse)
            {
                return EventSeverity.Low; // Some activity
            }
            else
            {
                return EventSeverity.Info; // No activity
            }
        }
    }
}
