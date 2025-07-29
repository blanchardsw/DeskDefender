using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services.Coordinators
{
    /// <summary>
    /// Aggregates and processes events from multiple monitoring sources
    /// Extracted from CompositeMonitoringService for better separation of concerns
    /// Implements intelligent event filtering and correlation to reduce noise
    /// </summary>
    public class EventAggregator
    {
        private readonly ILogger<EventAggregator> _logger;
        private readonly IEventLogger _eventLogger;
        private readonly IAlertService _alertService;
        
        // Event aggregation for intelligent alerting
        private readonly List<EventLog> _recentEvents = new List<EventLog>();
        private readonly TimeSpan _eventAggregationWindow = TimeSpan.FromMinutes(5);
        private DateTime _lastEventCleanup = DateTime.Now;
        private readonly object _eventLock = new object();

        public EventAggregator(
            ILogger<EventAggregator> logger,
            IEventLogger eventLogger,
            IAlertService alertService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
            _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        }

        /// <summary>
        /// Processes an incoming event with intelligent aggregation and filtering
        /// </summary>
        public async Task ProcessEventAsync(EventLog eventLog)
        {
            if (eventLog == null) return;

            try
            {
                // Clean up old events periodically
                CleanupOldEvents();

                // Add to recent events for correlation
                lock (_eventLock)
                {
                    _recentEvents.Add(eventLog);
                }

                // Apply intelligent filtering to reduce noise
                var shouldProcess = ShouldProcessEvent(eventLog);
                if (!shouldProcess)
                {
                    _logger.LogDebug("Event filtered out due to aggregation rules: {EventType}", eventLog.EventType);
                    return;
                }

                // Log the event
                await _eventLogger.LogAsync(eventLog);
                _logger.LogDebug("Event processed and logged: {EventType} - {Description}", 
                    eventLog.EventType, eventLog.Description);

                // Determine if alert should be sent based on severity and correlation
                if (ShouldSendAlert(eventLog))
                {
                    await SendAlertAsync(eventLog);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event: {EventType}", eventLog.EventType);
            }
        }

        /// <summary>
        /// Determines if an event should be processed based on aggregation rules
        /// </summary>
        private bool ShouldProcessEvent(EventLog eventLog)
        {
            lock (_eventLock)
            {
                // Always process high severity events
                if (eventLog.Severity >= EventSeverity.High)
                {
                    return true;
                }

                // Check for duplicate events within the aggregation window
                var duplicateCount = _recentEvents.Count(e => 
                    e.EventType == eventLog.EventType &&
                    e.Description == eventLog.Description &&
                    (DateTime.Now - e.Timestamp) <= _eventAggregationWindow);

                // Allow first occurrence and then limit frequency
                if (duplicateCount == 0)
                {
                    return true; // First occurrence
                }

                // For low severity events, limit to one per aggregation window
                if (eventLog.Severity <= EventSeverity.Low && duplicateCount >= 1)
                {
                    return false;
                }

                // For medium severity, allow up to 3 per window
                if (eventLog.Severity == EventSeverity.Medium && duplicateCount >= 3)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Determines if an alert should be sent for the event
        /// </summary>
        private bool ShouldSendAlert(EventLog eventLog)
        {
            // Always alert for critical events
            if (eventLog.Severity == EventSeverity.Critical)
            {
                return true;
            }

            // Alert for high severity events if not too frequent
            if (eventLog.Severity == EventSeverity.High)
            {
                lock (_eventLock)
                {
                    var recentHighSeverityAlerts = _recentEvents.Count(e => 
                        e.Severity >= EventSeverity.High &&
                        e.AlertSent &&
                        (DateTime.Now - e.Timestamp) <= TimeSpan.FromMinutes(10));

                    // Limit high severity alerts to prevent spam
                    return recentHighSeverityAlerts < 5;
                }
            }

            // Check for correlated events that might indicate a security issue
            if (HasCorrelatedSecurityEvents(eventLog))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks for correlated events that might indicate a security issue
        /// </summary>
        private bool HasCorrelatedSecurityEvents(EventLog eventLog)
        {
            lock (_eventLock)
            {
                var recentSecurityEvents = _recentEvents.Where(e => 
                    (DateTime.Now - e.Timestamp) <= TimeSpan.FromMinutes(15) &&
                    (e.EventType == "Login" || e.EventType == "Session" || e.EventType == "Input"))
                    .ToList();

                // Look for patterns that might indicate suspicious activity
                var failedLogins = recentSecurityEvents.Count(e => 
                    e.EventType == "Login" && e.Description.Contains("Failure"));

                var sessionChanges = recentSecurityEvents.Count(e => 
                    e.EventType == "Session");

                var inputActivity = recentSecurityEvents.Count(e => 
                    e.EventType == "Input");

                // Alert if multiple failed logins
                if (failedLogins >= 3)
                {
                    _logger.LogWarning("Multiple failed login attempts detected: {Count}", failedLogins);
                    return true;
                }

                // Alert if unusual session activity combined with input
                if (sessionChanges >= 2 && inputActivity >= 1)
                {
                    _logger.LogWarning("Suspicious session and input activity pattern detected");
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Sends an alert for the event
        /// </summary>
        private async Task SendAlertAsync(EventLog eventLog)
        {
            try
            {
                await _alertService.SendAlertAsync(eventLog);
                eventLog.AlertSent = true;
                _logger.LogInformation("Alert sent for event: {EventType} - {Description}", 
                    eventLog.EventType, eventLog.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send alert for event: {EventType}", eventLog.EventType);
            }
        }

        /// <summary>
        /// Cleans up old events from the aggregation window
        /// </summary>
        private void CleanupOldEvents()
        {
            if ((DateTime.Now - _lastEventCleanup) <= TimeSpan.FromMinutes(1))
            {
                return; // Only cleanup once per minute
            }

            lock (_eventLock)
            {
                var cutoffTime = DateTime.Now - _eventAggregationWindow;
                var eventsToRemove = _recentEvents.Where(e => e.Timestamp < cutoffTime).ToList();
                
                foreach (var eventToRemove in eventsToRemove)
                {
                    _recentEvents.Remove(eventToRemove);
                }

                if (eventsToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} old events from aggregation window", eventsToRemove.Count);
                }

                _lastEventCleanup = DateTime.Now;
            }
        }

        /// <summary>
        /// Gets statistics about recent event processing
        /// </summary>
        public EventAggregationStats GetStats()
        {
            lock (_eventLock)
            {
                var now = DateTime.Now;
                var last5Minutes = _recentEvents.Where(e => (now - e.Timestamp) <= TimeSpan.FromMinutes(5)).ToList();
                var last15Minutes = _recentEvents.Where(e => (now - e.Timestamp) <= TimeSpan.FromMinutes(15)).ToList();

                return new EventAggregationStats
                {
                    TotalRecentEvents = _recentEvents.Count,
                    EventsLast5Minutes = last5Minutes.Count,
                    EventsLast15Minutes = last15Minutes.Count,
                    AlertsSentLast5Minutes = last5Minutes.Count(e => e.AlertSent),
                    AlertsSentLast15Minutes = last15Minutes.Count(e => e.AlertSent),
                    EventTypeBreakdown = _recentEvents
                        .GroupBy(e => e.EventType)
                        .ToDictionary(g => g.Key, g => g.Count())
                };
            }
        }
    }

    /// <summary>
    /// Statistics about event aggregation and processing
    /// </summary>
    public class EventAggregationStats
    {
        public int TotalRecentEvents { get; set; }
        public int EventsLast5Minutes { get; set; }
        public int EventsLast15Minutes { get; set; }
        public int AlertsSentLast5Minutes { get; set; }
        public int AlertsSentLast15Minutes { get; set; }
        public Dictionary<string, int> EventTypeBreakdown { get; set; } = new Dictionary<string, int>();
    }
}
