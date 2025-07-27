using System;
using DeskDefender.Models.Events;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for monitoring Windows login attempts
    /// </summary>
    public interface ILoginMonitor : IMonitorService
    {
        /// <summary>
        /// Event fired when a login attempt is detected
        /// </summary>
        event EventHandler<LoginEvent> LoginAttemptDetected;

        /// <summary>
        /// Sets which event IDs to monitor (e.g., 4625 for failed logins)
        /// </summary>
        /// <param name="eventIds">Array of Windows Event Log IDs to monitor</param>
        void SetMonitoredEventIds(int[] eventIds);
    }
}
