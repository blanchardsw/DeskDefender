using System;
using System.Collections.Generic;
using DeskDefender.Models.Events;

namespace DeskDefender.Models.Alerts
{
    /// <summary>
    /// Represents a summary of events for alert notifications
    /// </summary>
    public class AlertSummary
    {
        /// <summary>
        /// Start time of the summary period
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// End time of the summary period
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Total number of events in the summary period
        /// </summary>
        public int TotalEvents { get; set; }

        /// <summary>
        /// Number of critical events
        /// </summary>
        public int CriticalEvents { get; set; }

        /// <summary>
        /// Number of high priority events
        /// </summary>
        public int HighEvents { get; set; }

        /// <summary>
        /// Number of medium priority events
        /// </summary>
        public int MediumEvents { get; set; }

        /// <summary>
        /// Number of low priority events
        /// </summary>
        public int LowEvents { get; set; }

        /// <summary>
        /// Number of info events
        /// </summary>
        public int InfoEvents { get; set; }

        /// <summary>
        /// Number of login events
        /// </summary>
        public int LoginEvents { get; set; }

        /// <summary>
        /// Number of input events (keyboard/mouse)
        /// </summary>
        public int InputEvents { get; set; }

        /// <summary>
        /// Number of session events (lock/unlock)
        /// </summary>
        public int SessionEvents { get; set; }

        /// <summary>
        /// Number of camera/capture events
        /// </summary>
        public int CameraEvents { get; set; }

        /// <summary>
        /// Number of system events
        /// </summary>
        public int SystemEvents { get; set; }

        /// <summary>
        /// Top events to highlight in the summary (most critical/important)
        /// </summary>
        public List<EventLog> TopEvents { get; set; } = new List<EventLog>();

        /// <summary>
        /// Whether this summary contains any critical or high priority events
        /// </summary>
        public bool HasCriticalEvents => CriticalEvents > 0 || HighEvents > 0;

        /// <summary>
        /// Whether this summary is worth sending (has significant events)
        /// </summary>
        public bool IsSignificant => TotalEvents > 0 && (HasCriticalEvents || TotalEvents >= 5);

        /// <summary>
        /// Gets a brief description of the summary period
        /// </summary>
        public string PeriodDescription
        {
            get
            {
                var duration = EndTime - StartTime;
                if (duration.TotalDays >= 1)
                    return $"{duration.Days} day(s)";
                else if (duration.TotalHours >= 1)
                    return $"{duration.Hours} hour(s)";
                else
                    return $"{duration.Minutes} minute(s)";
            }
        }

        /// <summary>
        /// Gets the highest severity level in this summary
        /// </summary>
        public string HighestSeverity
        {
            get
            {
                if (CriticalEvents > 0) return "Critical";
                if (HighEvents > 0) return "High";
                if (MediumEvents > 0) return "Medium";
                if (LowEvents > 0) return "Low";
                return "Info";
            }
        }
    }
}
