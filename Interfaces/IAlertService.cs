using System;
using System.Threading.Tasks;
using DeskDefender.Models.Alerts;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for coordinating SMS and email alerts
    /// </summary>
    public interface IAlertService
    {
        /// <summary>
        /// Starts the alert service and begins monitoring for events to summarize
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops the alert service
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Sends an immediate alert summary
        /// </summary>
        /// <param name="summary">Alert summary to send</param>
        Task SendAlertSummaryAsync(AlertSummary summary);

        /// <summary>
        /// Generates an alert summary for the specified time period
        /// </summary>
        /// <param name="startTime">Start time for summary</param>
        /// <param name="endTime">End time for summary</param>
        /// <returns>Alert summary or null if no significant events</returns>
        Task<AlertSummary?> GenerateAlertSummaryAsync(DateTime startTime, DateTime endTime);

        /// <summary>
        /// Sends test alerts to verify SMS and email configuration
        /// </summary>
        /// <returns>True if both test alerts were sent successfully</returns>
        Task<bool> SendTestAlertsAsync();

        /// <summary>
        /// Checks if alert service is properly configured
        /// </summary>
        /// <returns>True if either SMS or email alerts are configured</returns>
        bool IsConfigured();

        /// <summary>
        /// Event fired when an alert summary is sent
        /// </summary>
        event EventHandler<AlertSummary> AlertSent;

        /// <summary>
        /// Event fired when alert sending fails
        /// </summary>
        event EventHandler<string> AlertFailed;
    }
}
