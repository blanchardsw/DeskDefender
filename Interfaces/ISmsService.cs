using System.Threading.Tasks;
using DeskDefender.Models.Alerts;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for SMS messaging services
    /// </summary>
    public interface ISmsService
    {
        /// <summary>
        /// Sends an SMS alert with event summary
        /// </summary>
        /// <param name="phoneNumber">Destination phone number in E.164 format</param>
        /// <param name="summary">Alert summary to send</param>
        /// <returns>True if SMS was sent successfully</returns>
        Task<bool> SendAlertSummaryAsync(string phoneNumber, AlertSummary summary);

        /// <summary>
        /// Sends a test SMS to verify configuration
        /// </summary>
        /// <param name="phoneNumber">Destination phone number</param>
        /// <returns>True if test SMS was sent successfully</returns>
        Task<bool> SendTestSmsAsync(string phoneNumber);

        /// <summary>
        /// Validates SMS service configuration
        /// </summary>
        /// <returns>True if SMS service is properly configured</returns>
        bool IsConfigured();
    }
}
