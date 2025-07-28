using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DeskDefender.Models.Events;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for logging and retrieving events
    /// </summary>
    public interface IEventLogger
    {
        /// <summary>
        /// Logs an event asynchronously
        /// </summary>
        /// <param name="eventLog">The event to log</param>
        Task LogAsync(EventLog eventLog);

        /// <summary>
        /// Retrieves events within a date range
        /// </summary>
        /// <param name="startDate">Start date for the query</param>
        /// <param name="endDate">End date for the query</param>
        /// <returns>Collection of events</returns>
        Task<IEnumerable<EventLog>> GetEventsAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Retrieves events by type
        /// </summary>
        /// <param name="eventType">Type of events to retrieve</param>
        /// <returns>Collection of events</returns>
        Task<IEnumerable<EventLog>> GetEventsByTypeAsync(string eventType);

        /// <summary>
        /// Clears old events based on retention policy
        /// </summary>
        /// <param name="olderThan">Delete events older than this date</param>
        Task ClearOldEventsAsync(DateTime olderThan);

        /// <summary>
        /// Deletes events before a specific date
        /// </summary>
        /// <param name="beforeDate">Delete events before this date</param>
        Task DeleteEventsBeforeDateAsync(DateTime beforeDate);

        /// <summary>
        /// Logs an event asynchronously (alternative method name)
        /// </summary>
        /// <param name="eventLog">The event to log</param>
        Task LogEventAsync(EventLog eventLog);
    }
}
