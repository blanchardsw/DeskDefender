namespace DeskDefender.Models.Events
{
    /// <summary>
    /// Severity levels for security events
    /// Used to categorize the importance and urgency of detected events
    /// </summary>
    public enum EventSeverity
    {
        /// <summary>
        /// Informational events - normal system activity
        /// </summary>
        Info = 0,

        /// <summary>
        /// Low priority events - minor anomalies or routine activities
        /// </summary>
        Low = 1,

        /// <summary>
        /// Medium priority events - suspicious activity that warrants attention
        /// </summary>
        Medium = 2,

        /// <summary>
        /// Warning events - potential issues that should be monitored
        /// </summary>
        Warning = 2,

        /// <summary>
        /// High priority events - likely security threats requiring immediate attention
        /// </summary>
        High = 3,

        /// <summary>
        /// Critical events - confirmed security breaches or system compromises
        /// </summary>
        Critical = 4
    }
}
