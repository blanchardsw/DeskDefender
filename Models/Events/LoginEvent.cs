using System;

namespace DeskDefender.Models.Events
{
    /// <summary>
    /// Event model for login attempts detected through Windows Event Logs
    /// </summary>
    public class LoginEvent : EventLog
    {
        public LoginEvent()
        {
            EventType = "Login";
            Source = "LoginMonitor";
        }

        /// <summary>
        /// Username involved in the login attempt
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Whether the login attempt was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Windows Event Log ID that triggered this event
        /// </summary>
        public int EventId { get; set; }

        /// <summary>
        /// Source IP address of the login attempt (if available)
        /// </summary>
        public string? SourceIpAddress { get; set; }

        /// <summary>
        /// Workstation name from which the login was attempted
        /// </summary>
        public string WorkstationName { get; set; } = string.Empty;

        /// <summary>
        /// Logon type (Interactive, Network, Service, etc.)
        /// </summary>
        public string LogonType { get; set; } = string.Empty;

        /// <summary>
        /// Failure reason for unsuccessful login attempts
        /// </summary>
        public string? FailureReason { get; set; }
    }
}
