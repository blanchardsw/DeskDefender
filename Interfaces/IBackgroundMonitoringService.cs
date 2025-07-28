using System;
using DeskDefender.Interfaces;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for coordinating background monitoring services during session state changes
    /// </summary>
    public interface IBackgroundMonitoringService
    {
        /// <summary>
        /// Event fired when background monitoring status changes
        /// </summary>
        event EventHandler<BackgroundMonitoringStatusEventArgs>? StatusChanged;

        /// <summary>
        /// Gets whether background monitoring is currently active
        /// </summary>
        bool IsBackgroundMonitoringActive { get; }

        /// <summary>
        /// Ensures continuous monitoring across all services
        /// </summary>
        void EnsureContinuousMonitoring();

        /// <summary>
        /// Handles session state changes and adjusts monitoring accordingly
        /// </summary>
        /// <param name="newState">The new session state</param>
        void HandleSessionStateChange(SessionState newState);

        /// <summary>
        /// Starts background monitoring coordination
        /// </summary>
        void StartBackgroundMonitoring();

        /// <summary>
        /// Stops background monitoring coordination
        /// </summary>
        void StopBackgroundMonitoring();

        /// <summary>
        /// Gets the current status of all monitoring services
        /// </summary>
        /// <returns>Status information for all services</returns>
        BackgroundMonitoringStatus GetMonitoringStatus();
    }

    /// <summary>
    /// Event arguments for background monitoring status changes
    /// </summary>
    public class BackgroundMonitoringStatusEventArgs : EventArgs
    {
        /// <summary>
        /// The current monitoring status
        /// </summary>
        public BackgroundMonitoringStatus Status { get; set; } = new();

        /// <summary>
        /// Timestamp of the status change
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Additional context about the status change
        /// </summary>
        public string? Context { get; set; }
    }

    /// <summary>
    /// Status information for background monitoring services
    /// </summary>
    public class BackgroundMonitoringStatus
    {
        /// <summary>
        /// Whether input monitoring is active
        /// </summary>
        public bool InputMonitoringActive { get; set; }

        /// <summary>
        /// Whether camera monitoring is active
        /// </summary>
        public bool CameraMonitoringActive { get; set; }

        /// <summary>
        /// Whether session monitoring is active
        /// </summary>
        public bool SessionMonitoringActive { get; set; }

        /// <summary>
        /// Whether event logging is active
        /// </summary>
        public bool EventLoggingActive { get; set; }

        /// <summary>
        /// Current session state
        /// </summary>
        public SessionState CurrentSessionState { get; set; }

        /// <summary>
        /// Whether the session is currently locked
        /// </summary>
        public bool IsSessionLocked { get; set; }

        /// <summary>
        /// Number of active monitoring services
        /// </summary>
        public int ActiveServicesCount => 
            (InputMonitoringActive ? 1 : 0) +
            (CameraMonitoringActive ? 1 : 0) +
            (SessionMonitoringActive ? 1 : 0) +
            (EventLoggingActive ? 1 : 0);

        /// <summary>
        /// Whether all critical services are running
        /// </summary>
        public bool AllCriticalServicesActive => 
            InputMonitoringActive && SessionMonitoringActive && EventLoggingActive;

        /// <summary>
        /// Last status update timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
