using System;

namespace DeskDefender.Models.Events
{
    /// <summary>
    /// Base class for all system events
    /// </summary>
    public class EventLog
    {
        /// <summary>
        /// Unique identifier for the event
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Timestamp when the event occurred
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Type of event (Input, Motion, Login, USB, etc.)
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// Detailed description of the event
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Severity level of the event
        /// </summary>
        public EventSeverity Severity { get; set; } = EventSeverity.Info;

        /// <summary>
        /// Path to associated image file (if any)
        /// </summary>
        public string ImagePath { get; set; }

        /// <summary>
        /// Additional metadata as JSON string
        /// </summary>
        public string Metadata { get; set; }

        /// <summary>
        /// Whether this event triggered an alert
        /// </summary>
        public bool AlertSent { get; set; }

        /// <summary>
        /// Source component that generated the event
        /// </summary>
        public string Source { get; set; }
    }

    // EventSeverity enum is defined in EventSeverity.cs to avoid duplication
}
