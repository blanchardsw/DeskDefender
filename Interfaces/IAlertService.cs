using System.Threading.Tasks;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for sending alerts through various channels
    /// </summary>
    public interface IAlertService
    {
        /// <summary>
        /// Sends an alert message
        /// </summary>
        /// <param name="message">The alert message</param>
        /// <param name="imagePath">Optional path to an image attachment</param>
        Task SendAlertAsync(string message, string imagePath = null);

        /// <summary>
        /// Tests the alert service configuration
        /// </summary>
        /// <returns>True if the service is properly configured and can send alerts</returns>
        Task<bool> TestConnectionAsync();

        /// <summary>
        /// Gets the display name of the alert service
        /// </summary>
        string ServiceName { get; }
    }
}
