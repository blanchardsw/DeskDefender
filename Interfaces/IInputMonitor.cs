using System;
using DeskDefender.Models.Events;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for monitoring keyboard and mouse input
    /// </summary>
    public interface IInputMonitor : IMonitorService
    {
        /// <summary>
        /// Event fired when input activity is detected
        /// </summary>
        event EventHandler<InputEvent> InputDetected;

        /// <summary>
        /// Gets the current idle time of the system
        /// </summary>
        TimeSpan GetIdleTime();

        /// <summary>
        /// Sets the sensitivity threshold for input detection
        /// </summary>
        /// <param name="threshold">Minimum time between inputs to trigger an event</param>
        void SetSensitivity(TimeSpan threshold);
    }
}
