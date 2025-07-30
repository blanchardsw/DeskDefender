namespace DeskDefender.Models.Events
{
    /// <summary>
    /// Enumeration of severity levels for consistent string handling
    /// Replaces hard-coded severity strings for better maintainability
    /// </summary>
    public enum SeverityLevel
    {
        /// <summary>
        /// Unknown severity level
        /// </summary>
        Unknown = -1,

        /// <summary>
        /// Informational events showing normal system activity
        /// </summary>
        Info = 0,

        /// <summary>
        /// Minor anomalies or routine activities with low priority
        /// </summary>
        Low = 1,

        /// <summary>
        /// Medium priority events or warnings
        /// </summary>
        Medium = 2,

        /// <summary>
        /// High priority security events requiring attention
        /// </summary>
        High = 3,

        /// <summary>
        /// Critical security events requiring immediate attention
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// Extension methods for SeverityLevel enum
    /// </summary>
    public static class SeverityLevelExtensions
    {
        /// <summary>
        /// Converts SeverityLevel enum to display string
        /// </summary>
        public static string ToDisplayString(this SeverityLevel severity)
        {
            return severity switch
            {
                SeverityLevel.Critical => "Critical",
                SeverityLevel.High => "High",
                SeverityLevel.Medium => "Medium",
                SeverityLevel.Low => "Low",
                SeverityLevel.Info => "Info",
                SeverityLevel.Unknown => "Unknown",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Parses string to SeverityLevel enum
        /// </summary>
        public static SeverityLevel FromString(string severityString)
        {
            return severityString switch
            {
                "Critical" => SeverityLevel.Critical,
                "High" => SeverityLevel.High,
                "Medium" => SeverityLevel.Medium,
                "Warning" => SeverityLevel.Medium, // Alias for Medium
                "Low" => SeverityLevel.Low,
                "Info" => SeverityLevel.Info,
                _ => SeverityLevel.Unknown
            };
        }

        /// <summary>
        /// Gets the appropriate color for severity level display
        /// Matches UIHelper.GetSeverityBrush() logic
        /// </summary>
        public static System.Windows.Media.Color GetSeverityColor(this SeverityLevel severity)
        {
            return severity switch
            {
                SeverityLevel.Critical => System.Windows.Media.Colors.Red,
                SeverityLevel.High => System.Windows.Media.Colors.Orange,
                SeverityLevel.Medium => System.Windows.Media.Colors.Yellow,
                SeverityLevel.Low => System.Windows.Media.Colors.Green,
                SeverityLevel.Info => System.Windows.Media.Colors.LightGray,
                SeverityLevel.Unknown => System.Windows.Media.Colors.Gray,
                _ => System.Windows.Media.Colors.Gray
            };
        }

        /// <summary>
        /// Gets SeverityLevel from color (for filter logic)
        /// </summary>
        public static SeverityLevel FromColor(System.Windows.Media.Color color)
        {
            if (color == System.Windows.Media.Colors.Red) return SeverityLevel.Critical;
            if (color == System.Windows.Media.Colors.Orange) return SeverityLevel.High;
            if (color == System.Windows.Media.Colors.Yellow) return SeverityLevel.Medium;
            if (color == System.Windows.Media.Colors.Green) return SeverityLevel.Low;
            if (color == System.Windows.Media.Colors.LightGray) return SeverityLevel.Info;
            if (color == System.Windows.Media.Colors.Gray) return SeverityLevel.Info;
            return SeverityLevel.Unknown;
        }

        /// <summary>
        /// Converts EventSeverity enum to SeverityLevel enum
        /// </summary>
        public static SeverityLevel ToSeverityLevel(this EventSeverity eventSeverity)
        {
            return eventSeverity switch
            {
                EventSeverity.Critical => SeverityLevel.Critical,
                EventSeverity.High => SeverityLevel.High,
                EventSeverity.Medium => SeverityLevel.Medium,
                EventSeverity.Low => SeverityLevel.Low,
                EventSeverity.Info => SeverityLevel.Info,
                _ => SeverityLevel.Unknown
            };
        }

        /// <summary>
        /// Converts SeverityLevel enum to EventSeverity enum
        /// </summary>
        public static EventSeverity ToEventSeverity(this SeverityLevel severityLevel)
        {
            return severityLevel switch
            {
                SeverityLevel.Critical => EventSeverity.Critical,
                SeverityLevel.High => EventSeverity.High,
                SeverityLevel.Medium => EventSeverity.Medium,
                SeverityLevel.Low => EventSeverity.Low,
                SeverityLevel.Info => EventSeverity.Info,
                _ => EventSeverity.Info
            };
        }
    }
}
