using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using DeskDefender.Models.Alerts;
using DeskDefender.Models.Events;
using DeskDefender.Models.Settings;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Service responsible for aggregating events into alert summaries
    /// Follows Single Responsibility Principle - only handles event aggregation
    /// </summary>
    public class EventAggregationService : IEventAggregationService
    {
        private readonly ILogger<EventAggregationService> _logger;
        private readonly IEventLogger _eventLogger;

        public EventAggregationService(
            ILogger<EventAggregationService> logger,
            IEventLogger eventLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
        }

        /// <summary>
        /// Aggregates events from the specified time period into an alert summary
        /// </summary>
        public async Task<AlertSummary> AggregateEventsAsync(DateTime startTime, DateTime endTime, AlertSettings settings)
        {
            try
            {
                _logger.LogDebug("Aggregating events from {StartTime} to {EndTime}", startTime, endTime);

                var events = await _eventLogger.GetEventsAsync(startTime, endTime);
                var filteredEvents = FilterEventsBySeverity(events, settings.MinimumAlertSeverity);

                if (!settings.IncludeSystemEventsInAlerts)
                {
                    filteredEvents = FilterOutSystemEvents(filteredEvents);
                }

                var summary = CreateAlertSummary(filteredEvents, startTime, endTime, settings.MaxEventsPerAlert);
                
                _logger.LogDebug("Aggregated {TotalEvents} events into summary with {SignificantEvents} significant events", 
                    events.Count(), summary.TotalEvents);

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to aggregate events from {StartTime} to {EndTime}", startTime, endTime);
                throw;
            }
        }

        /// <summary>
        /// Filters events by minimum severity level
        /// </summary>
        private IEnumerable<EventLog> FilterEventsBySeverity(IEnumerable<EventLog> events, SeverityLevel minimumSeverity)
        {
            return events.Where(evt => 
            {
                var eventSeverity = evt.Severity.ToSeverityLevel();
                if (eventSeverity != SeverityLevel.Unknown)
                {
                    return (int)eventSeverity >= (int)minimumSeverity;
                }
                return false; // Exclude events with unknown severity
            });
        }

        /// <summary>
        /// Filters out system events from the collection
        /// </summary>
        private IEnumerable<EventLog> FilterOutSystemEvents(IEnumerable<EventLog> events)
        {
            return events.Where(evt => 
            {
                var eventType = EventTypeExtensions.FromString(evt.EventType);
                if (eventType != EventType.Unknown)
                {
                    return !eventType.IsSystemEvent();
                }
                return true; // Include events with unknown type to be safe
            });
        }

        /// <summary>
        /// Creates an alert summary from filtered events
        /// </summary>
        private AlertSummary CreateAlertSummary(IEnumerable<EventLog> events, DateTime startTime, DateTime endTime, int maxEvents)
        {
            var eventList = events.ToList();
            var summary = new AlertSummary
            {
                StartTime = startTime,
                EndTime = endTime,
                TotalEvents = eventList.Count
            };

            // Count events by severity
            foreach (var evt in eventList)
            {
                var severity = evt.Severity.ToSeverityLevel();
                if (severity != SeverityLevel.Unknown)
                {
                    switch (severity)
                    {
                        case SeverityLevel.Critical:
                            summary.CriticalEvents++;
                            break;
                        case SeverityLevel.High:
                            summary.HighEvents++;
                            break;
                        case SeverityLevel.Medium:
                            summary.MediumEvents++;
                            break;
                        case SeverityLevel.Low:
                            summary.LowEvents++;
                            break;
                        case SeverityLevel.Info:
                            summary.InfoEvents++;
                            break;
                    }
                }
            }

            // Add most significant events (limited by maxEvents)
            var topEvents = eventList
                .OrderBy(evt => GetSeverityPriority(evt.Severity.ToString()))
                .ThenByDescending(evt => evt.Timestamp)
                .Take(maxEvents)
                .ToList();

            summary.TopEvents = topEvents;
            // IsSignificant is computed automatically based on event counts

            return summary;
        }

        /// <summary>
        /// Gets numeric priority for severity (lower number = higher priority)
        /// </summary>
        private int GetSeverityPriority(string severity)
        {
            // Convert EventSeverity enum to SeverityLevel enum
            var eventSeverity = (EventSeverity)Enum.Parse(typeof(EventSeverity), severity, true);
            var severityLevel = eventSeverity.ToSeverityLevel();
            if (severityLevel != SeverityLevel.Unknown)
            {
                return (int)severityLevel;
            }
            return 5; // Unknown severity gets lowest priority
        }

        /// <summary>
        /// Determines if the alert summary is significant enough to send
        /// </summary>
        private bool DetermineSignificance(AlertSummary summary)
        {
            // Always significant if there are critical or high severity events
            if (summary.CriticalEvents > 0 || summary.HighEvents > 0)
                return true;

            // Significant if there are multiple medium severity events
            if (summary.MediumEvents >= 3)
                return true;

            // Significant if there are many total events
            if (summary.TotalEvents >= 10)
                return true;

            return false;
        }
    }
}
