using System;
using System.Threading.Tasks;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for alert scheduling service
    /// Follows Interface Segregation Principle - focused only on scheduling
    /// </summary>
    public interface IAlertSchedulingService
    {
        /// <summary>
        /// Event fired when alert interval elapses
        /// </summary>
        event EventHandler? AlertIntervalElapsed;

        /// <summary>
        /// Starts the alert scheduling timer
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops the alert scheduling timer
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Gets the time range for the last alert interval
        /// </summary>
        (DateTime StartTime, DateTime EndTime) GetLastAlertInterval();

        /// <summary>
        /// Updates the last alert time to the current time
        /// </summary>
        void UpdateLastAlertTime();

        /// <summary>
        /// Gets whether the scheduling service is currently running
        /// </summary>
        bool IsRunning { get; }
    }
}
