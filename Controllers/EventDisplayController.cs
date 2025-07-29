using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Controllers
{
    /// <summary>
    /// Handles event display logic and UI updates for the main window
    /// Extracted from MainWindow to improve separation of concerns
    /// </summary>
    public class EventDisplayController
    {
        private readonly ILogger<EventDisplayController> _logger;
        private readonly IEventLogger _eventLogger;
        private readonly ObservableCollection<EventDisplayModel> _recentEvents;
        private readonly ObservableCollection<EventDisplayModel> _eventLog;
        private readonly object _eventLogLock = new object();
        private readonly Dispatcher _dispatcher;

        public EventDisplayController(
            ILogger<EventDisplayController> logger,
            IEventLogger eventLogger,
            ObservableCollection<EventDisplayModel> recentEvents,
            ObservableCollection<EventDisplayModel> eventLog,
            Dispatcher dispatcher)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
            _recentEvents = recentEvents ?? throw new ArgumentNullException(nameof(recentEvents));
            _eventLog = eventLog ?? throw new ArgumentNullException(nameof(eventLog));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>
        /// Handles incoming events and updates the UI collections
        /// </summary>
        public void HandleEventReceived(EventLog eventLog)
        {
            try
            {
                // Update UI on the UI thread
                _dispatcher.BeginInvoke(() =>
                {
                    var displayModel = new EventDisplayModel(eventLog);
                    
                    // Add to recent events (limit to 10 most recent)
                    _recentEvents.Insert(0, displayModel);
                    while (_recentEvents.Count > 10)
                    {
                        _recentEvents.RemoveAt(_recentEvents.Count - 1);
                    }
                    
                    // Add to full event log
                    _eventLog.Insert(0, displayModel);
                    
                    _logger.LogInformation("Event added to UI: {EventType} - {Description}", 
                        eventLog.EventType, eventLog.Description);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling received event");
            }
        }

        /// <summary>
        /// Loads existing events from database into UI collections
        /// </summary>
        public async Task LoadEventLogAsync()
        {
            try
            {
                _logger.LogInformation("Loading existing events from database at startup...");
                
                // Get events from the last 7 days to populate the UI
                var startDate = DateTime.Now.AddDays(-7);
                var endDate = DateTime.Now;
                
                var existingEvents = await _eventLogger.GetEventsAsync(startDate, endDate);
                _logger.LogInformation("Retrieved {EventCount} existing events from database", existingEvents.Count());
                
                // Clear existing UI events and populate with database events
                await _dispatcher.BeginInvoke(() =>
                {
                    lock (_eventLogLock)
                    {
                        _eventLog.Clear();
                        _recentEvents.Clear();
                        
                        foreach (var eventLog in existingEvents.OrderByDescending(e => e.Timestamp))
                        {
                            var displayModel = new EventDisplayModel(eventLog);
                            _eventLog.Add(displayModel);
                            
                            // Add to recent events if within last hour
                            if (eventLog.Timestamp > DateTime.Now.AddHours(-1))
                            {
                                _recentEvents.Add(displayModel);
                            }
                        }
                        
                        _logger.LogInformation("Loaded {UIEventCount} events into UI, {RecentCount} recent events", 
                            _eventLog.Count, _recentEvents.Count);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load existing events from database at startup");
                // Don't throw - app should continue even if event loading fails
            }
        }

        /// <summary>
        /// Applies filters to the event log and returns filtered results
        /// </summary>
        public void ApplyEventFilters(
            string? selectedEventType,
            string? selectedSeverity,
            int timeValue,
            string timeUnit,
            System.Windows.Controls.ListView eventLogList)
        {
            try
            {
                var filteredEvents = _eventLog.AsEnumerable();
                
                // Apply timestamp filter
                var cutoffTime = timeUnit switch
                {
                    "Minutes" => DateTime.Now.AddMinutes(-timeValue),
                    "Hours" => DateTime.Now.AddHours(-timeValue),
                    "Days" => DateTime.Now.AddDays(-timeValue),
                    _ => DateTime.Now.AddHours(-1)
                };
                
                filteredEvents = filteredEvents.Where(e => e.Timestamp >= cutoffTime);
                
                // Apply event type filter
                if (!string.IsNullOrEmpty(selectedEventType) && selectedEventType != "All")
                {
                    filteredEvents = filteredEvents.Where(e => e.EventType == selectedEventType);
                }
                
                // Apply severity filter
                if (!string.IsNullOrEmpty(selectedSeverity) && selectedSeverity != "All")
                {
                    filteredEvents = filteredEvents.Where(e => 
                        GetSeverityFromColor(e.SeverityColor) == selectedSeverity);
                }
                
                var resultList = filteredEvents.OrderByDescending(e => e.Timestamp).ToList();
                eventLogList.ItemsSource = resultList;
                
                _logger.LogDebug("Applied filters: EventType={EventType}, Severity={Severity}, TimeRange={TimeValue} {TimeUnit}, Results={Count}", 
                    selectedEventType, selectedSeverity, timeValue, timeUnit, resultList.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ApplyEventFilters");
                eventLogList.ItemsSource = _eventLog;
            }
        }

        /// <summary>
        /// Helper method to get severity string from color brush
        /// </summary>
        private string GetSeverityFromColor(System.Windows.Media.Brush brush)
        {
            // This is a simplified implementation - you might want to enhance this
            var color = ((System.Windows.Media.SolidColorBrush)brush).Color;
            if (color == System.Windows.Media.Colors.Red) return "Critical";
            if (color == System.Windows.Media.Colors.Orange) return "High";
            if (color == System.Windows.Media.Colors.Yellow) return "Medium";
            if (color == System.Windows.Media.Colors.LightBlue) return "Low";
            return "Info";
        }
    }
}
