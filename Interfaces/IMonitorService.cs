using System;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Base interface for all monitoring services
    /// </summary>
    public interface IMonitorService
    {
        /// <summary>
        /// Starts the monitoring service
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the monitoring service
        /// </summary>
        void Stop();

        /// <summary>
        /// Gets the current status of the monitoring service
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Event fired when the service status changes
        /// </summary>
        event EventHandler<bool> StatusChanged;
    }
}
