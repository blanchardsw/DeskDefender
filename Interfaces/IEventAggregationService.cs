using System;
using System.Threading.Tasks;
using DeskDefender.Models.Alerts;
using DeskDefender.Models.Settings;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for event aggregation service
    /// Follows Interface Segregation Principle - focused only on event aggregation
    /// </summary>
    public interface IEventAggregationService
    {
        /// <summary>
        /// Aggregates events from the specified time period into an alert summary
        /// </summary>
        /// <param name="startTime">Start time for event aggregation</param>
        /// <param name="endTime">End time for event aggregation</param>
        /// <param name="settings">Alert settings for filtering and configuration</param>
        /// <returns>Alert summary containing aggregated event data</returns>
        Task<AlertSummary> AggregateEventsAsync(DateTime startTime, DateTime endTime, AlertSettings settings);
    }
}
