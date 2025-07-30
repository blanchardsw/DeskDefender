using System.Threading.Tasks;
using DeskDefender.Models.Alerts;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for email messaging services
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Sends an email alert with event summary
        /// </summary>
        /// <param name="emailAddress">Destination email address</param>
        /// <param name="summary">Alert summary to send</param>
        /// <returns>True if email was sent successfully</returns>
        Task<bool> SendAlertSummaryAsync(string emailAddress, AlertSummary summary);

        /// <summary>
        /// Sends a test email to verify configuration
        /// </summary>
        /// <param name="emailAddress">Destination email address</param>
        /// <returns>True if test email was sent successfully</returns>
        Task<bool> SendTestEmailAsync(string emailAddress);

        /// <summary>
        /// Validates email service configuration
        /// </summary>
        /// <returns>True if email service is properly configured</returns>
        bool IsConfigured();
    }
}
