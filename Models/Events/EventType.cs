namespace DeskDefender.Models.Events
{
    /// <summary>
    /// Enumeration of all known event types in the DeskDefender application
    /// Replaces hard-coded strings for better maintainability and type safety
    /// </summary>
    public enum EventType
    {
        /// <summary>
        /// Unknown or unrecognized event type
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// User input events (keyboard, mouse)
        /// </summary>
        Input = 1,

        /// <summary>
        /// Login and authentication events
        /// </summary>
        Login = 2,

        /// <summary>
        /// Camera and webcam capture events
        /// </summary>
        Camera = 3,

        /// <summary>
        /// System-level events and notifications
        /// </summary>
        System = 4,

        /// <summary>
        /// Session state changes (lock/unlock)
        /// </summary>
        Session = 5,

        /// <summary>
        /// Background monitoring service events
        /// </summary>
        BackgroundMonitoring = 6,

        /// <summary>
        /// Motion detection events
        /// </summary>
        Motion = 7,

        /// <summary>
        /// USB device events
        /// </summary>
        USB = 8,

        /// <summary>
        /// Event summary aggregations
        /// </summary>
        EventSummary = 9,

        /// <summary>
        /// Screen capture events
        /// </summary>
        ScreenCapture = 10,

        /// <summary>
        /// Webcam capture events
        /// </summary>
        WebcamCapture = 11,

        /// <summary>
        /// Database maintenance events
        /// </summary>
        DatabaseMaintenance = 12,

        /// <summary>
        /// Service connection events
        /// </summary>
        ServiceConnection = 13,

        /// <summary>
        /// Service disconnection events
        /// </summary>
        ServiceDisconnection = 14,

        /// <summary>
        /// Input activity summary events
        /// </summary>
        InputActivitySummary = 15
    }

    /// <summary>
    /// Extension methods for EventType enum
    /// </summary>
    public static class EventTypeExtensions
    {
        /// <summary>
        /// Converts EventType enum to string representation
        /// </summary>
        public static string ToDisplayString(this EventType eventType)
        {
            return eventType switch
            {
                EventType.Input => "Input",
                EventType.Login => "Login",
                EventType.Camera => "Camera",
                EventType.System => "System",
                EventType.Session => "Session",
                EventType.BackgroundMonitoring => "BackgroundMonitoring",
                EventType.Motion => "Motion",
                EventType.USB => "USB",
                EventType.EventSummary => "Event Summary",
                EventType.ScreenCapture => "Screen Capture",
                EventType.WebcamCapture => "Webcam Capture",
                EventType.DatabaseMaintenance => "DatabaseMaintenance",
                EventType.ServiceConnection => "Service Connection",
                EventType.ServiceDisconnection => "Service Disconnection",
                EventType.InputActivitySummary => "Input Activity Summary",
                EventType.Unknown => "Unknown",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Parses string to EventType enum
        /// </summary>
        public static EventType FromString(string eventTypeString)
        {
            return eventTypeString switch
            {
                "Input" => EventType.Input,
                "Login" => EventType.Login,
                "Camera" => EventType.Camera,
                "System" => EventType.System,
                "Session" => EventType.Session,
                "BackgroundMonitoring" => EventType.BackgroundMonitoring,
                "Motion" => EventType.Motion,
                "USB" => EventType.USB,
                "Event Summary" => EventType.EventSummary,
                "Screen Capture" => EventType.ScreenCapture,
                "Webcam Capture" => EventType.WebcamCapture,
                "DatabaseMaintenance" => EventType.DatabaseMaintenance,
                "Service Connection" => EventType.ServiceConnection,
                "Service Disconnection" => EventType.ServiceDisconnection,
                "Input Activity Summary" => EventType.InputActivitySummary,
                _ => EventType.Unknown
            };
        }

        /// <summary>
        /// Determines if an event type is considered a system-level event
        /// </summary>
        public static bool IsSystemEvent(this EventType eventType)
        {
            return eventType switch
            {
                EventType.System => true,
                EventType.BackgroundMonitoring => true,
                EventType.Session => true,
                _ => false
            };
        }
    }
}
