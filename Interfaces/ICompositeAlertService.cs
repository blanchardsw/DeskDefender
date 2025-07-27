using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for managing multiple alert services (SMS, Email, etc.)
    /// </summary>
    public interface ICompositeAlertService : IAlertService
    {
        /// <summary>
        /// Adds an alert service to the composite
        /// </summary>
        /// <param name="alertService">Alert service to add</param>
        void AddAlertService(IAlertService alertService);

        /// <summary>
        /// Removes an alert service from the composite
        /// </summary>
        /// <param name="alertService">Alert service to remove</param>
        void RemoveAlertService(IAlertService alertService);

        /// <summary>
        /// Gets all registered alert services
        /// </summary>
        /// <returns>Collection of alert services</returns>
        IEnumerable<IAlertService> GetAlertServices();

        /// <summary>
        /// Tests all alert services
        /// </summary>
        /// <returns>Dictionary of service names and their test results</returns>
        Task<Dictionary<string, bool>> TestAllServicesAsync();
    }
}
