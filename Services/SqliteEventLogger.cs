using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DeskDefender.Data;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// SQLite-based implementation of event logging service
    /// Implements the Repository pattern for data access abstraction
    /// Uses Entity Framework Core for ORM capabilities
    /// Provides both synchronous and asynchronous operations for performance
    /// </summary>
    public class SqliteEventLogger : IEventLogger
    {
        #region Private Fields

        private readonly ILogger<SqliteEventLogger> _logger;
        private readonly IDbContextFactory<SecurityContext> _contextFactory;
        private readonly string _logDirectory;
        private readonly object _lockObject = new object();

        // Performance optimization - batch operations
        private readonly List<EventLog> _pendingEvents = new List<EventLog>();
        private readonly int _batchSize = 50;
        private DateTime _lastFlush = DateTime.Now;
        private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(30);

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the SqliteEventLogger
        /// Uses dependency injection for database context factory and logger
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts</param>
        /// <param name="logger">Logger instance for diagnostic information</param>
        public SqliteEventLogger(IDbContextFactory<SecurityContext> contextFactory, ILogger<SqliteEventLogger> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Ensure log directory exists
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Logs");
            Directory.CreateDirectory(_logDirectory);
            
            _logger.LogInformation("SqliteEventLogger initialized with log directory: {LogDirectory}", _logDirectory);
        }

        #endregion

        #region IEventLogger Implementation

        /// <summary>
        /// Logs a security event to the database
        /// Implements batching for performance optimization
        /// Uses the Unit of Work pattern for transaction management
        /// </summary>
        /// <param name="eventLog">The event to log</param>
        public void LogEvent(EventLog eventLog)
        {
            if (eventLog == null)
            {
                throw new ArgumentNullException(nameof(eventLog));
            }

            try
            {
                lock (_lockObject)
                {
                    // Add to pending batch
                    _pendingEvents.Add(eventLog);
                    
                    // Check if we should flush the batch
                    var shouldFlush = _pendingEvents.Count >= _batchSize ||
                                    DateTime.Now - _lastFlush >= _flushInterval ||
                                    eventLog.Severity >= EventSeverity.High; // Immediate flush for high severity

                    if (shouldFlush)
                    {
                        FlushPendingEvents();
                    }
                }

                _logger.LogDebug("Event queued for logging: {EventType} - {Description}", 
                    eventLog.EventType, eventLog.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging event: {EventType}", eventLog.EventType);
                
                // Fallback to file logging if database fails
                LogToFile(eventLog);
            }
        }

        /// <summary>
        /// Asynchronously logs a security event to the database
        /// Provides non-blocking operation for high-throughput scenarios
        /// </summary>
        /// <param name="eventLog">The event to log</param>
        public async Task LogEventAsync(EventLog eventLog)
        {
            if (eventLog == null)
            {
                throw new ArgumentNullException(nameof(eventLog));
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                // Add event to context
                context.Events.Add(eventLog);
                
                // Save changes asynchronously
                await context.SaveChangesAsync();
                
                _logger.LogDebug("Event logged asynchronously: {EventType} - {Description}", 
                    eventLog.EventType, eventLog.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging event asynchronously: {EventType}", eventLog.EventType);
                
                // Fallback to file logging
                LogToFile(eventLog);
            }
        }

        /// <summary>
        /// Retrieves events within a specified date range
        /// Implements the Repository pattern with filtering and pagination
        /// Uses Entity Framework's deferred execution for performance
        /// </summary>
        /// <param name="from">Start date for event retrieval</param>
        /// <param name="to">End date for event retrieval</param>
        /// <param name="eventTypes">Optional filter for specific event types</param>
        /// <param name="severities">Optional filter for specific severity levels</param>
        /// <param name="pageSize">Maximum number of events to return</param>
        /// <param name="pageNumber">Page number for pagination (0-based)</param>
        /// <returns>List of events matching the criteria</returns>
        public List<EventLog> GetEvents(DateTime from, DateTime to, 
            IEnumerable<string> eventTypes = null, 
            IEnumerable<EventSeverity> severities = null,
            int pageSize = 100, 
            int pageNumber = 0)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                
                var query = context.Events.AsQueryable();
                
                // Apply date range filter
                query = query.Where(e => e.Timestamp >= from && e.Timestamp <= to);
                
                // Apply event type filter if specified
                if (eventTypes != null && eventTypes.Any())
                {
                    var typeList = eventTypes.ToList();
                    query = query.Where(e => typeList.Contains(e.EventType));
                }
                
                // Apply severity filter if specified
                if (severities != null && severities.Any())
                {
                    var severityList = severities.ToList();
                    query = query.Where(e => severityList.Contains(e.Severity));
                }
                
                // Apply pagination and ordering
                var events = query
                    .OrderByDescending(e => e.Timestamp)
                    .Skip(pageNumber * pageSize)
                    .Take(pageSize)
                    .ToList();
                
                _logger.LogDebug("Retrieved {Count} events from {From} to {To}", 
                    events.Count, from, to);
                
                return events;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving events from {From} to {To}", from, to);
                return new List<EventLog>();
            }
        }

        /// <summary>
        /// Logs an event asynchronously (required by IEventLogger interface)
        /// </summary>
        /// <param name="eventLog">The event to log</param>
        public async Task LogAsync(EventLog eventLog)
        {
            await LogEventAsync(eventLog);
        }

        /// <summary>
        /// Retrieves events within a date range (required by IEventLogger interface)
        /// </summary>
        /// <param name="startDate">Start date for the query</param>
        /// <param name="endDate">End date for the query</param>
        /// <returns>Collection of events</returns>
        public async Task<IEnumerable<EventLog>> GetEventsAsync(DateTime startDate, DateTime endDate)
        {
            var events = await GetEventsAsync(startDate, endDate, null, null, 1000, 0);
            return events;
        }

        /// <summary>
        /// Retrieves events by type (required by IEventLogger interface)
        /// </summary>
        /// <param name="eventType">Type of events to retrieve</param>
        /// <returns>Collection of events</returns>
        public async Task<IEnumerable<EventLog>> GetEventsByTypeAsync(string eventType)
        {
            var events = await GetEventsAsync(DateTime.MinValue, DateTime.MaxValue, new[] { eventType }, null, 1000, 0);
            return events;
        }

        /// <summary>
        /// Clears old events based on retention policy (required by IEventLogger interface)
        /// </summary>
        /// <param name="olderThan">Delete events older than this date</param>
        public async Task ClearOldEventsAsync(DateTime olderThan)
        {
            await Task.Run(() =>
            {
                var retentionDays = (int)(DateTime.Now - olderThan).TotalDays;
                CleanupOldEvents(Math.Max(1, retentionDays));
            });
        }

        /// <summary>
        /// Asynchronously retrieves events within a specified date range
        /// Provides non-blocking operation for UI responsiveness
        /// </summary>
        public async Task<List<EventLog>> GetEventsAsync(DateTime from, DateTime to,
            IEnumerable<string> eventTypes = null,
            IEnumerable<EventSeverity> severities = null,
            int pageSize = 100,
            int pageNumber = 0)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                var query = context.Events.AsQueryable();
                
                // Apply filters (same logic as synchronous version)
                query = query.Where(e => e.Timestamp >= from && e.Timestamp <= to);
                
                if (eventTypes != null && eventTypes.Any())
                {
                    var typeList = eventTypes.ToList();
                    query = query.Where(e => typeList.Contains(e.EventType));
                }
                
                if (severities != null && severities.Any())
                {
                    var severityList = severities.ToList();
                    query = query.Where(e => severityList.Contains(e.Severity));
                }
                
                // Execute query asynchronously
                var events = await query
                    .OrderByDescending(e => e.Timestamp)
                    .Skip(pageNumber * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
                
                _logger.LogDebug("Retrieved {Count} events asynchronously from {From} to {To}", 
                    events.Count, from, to);
                
                return events;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving events asynchronously from {From} to {To}", from, to);
                return new List<EventLog>();
            }
        }

        /// <summary>
        /// Gets the total count of events matching the specified criteria
        /// Useful for pagination and statistics
        /// </summary>
        public int GetEventCount(DateTime from, DateTime to,
            IEnumerable<string> eventTypes = null,
            IEnumerable<EventSeverity> severities = null)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                
                var query = context.Events.AsQueryable();
                
                // Apply filters
                query = query.Where(e => e.Timestamp >= from && e.Timestamp <= to);
                
                if (eventTypes != null && eventTypes.Any())
                {
                    var typeList = eventTypes.ToList();
                    query = query.Where(e => typeList.Contains(e.EventType));
                }
                
                if (severities != null && severities.Any())
                {
                    var severityList = severities.ToList();
                    query = query.Where(e => severityList.Contains(e.Severity));
                }
                
                return query.Count();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting event count from {From} to {To}", from, to);
                return 0;
            }
        }

        /// <summary>
        /// Deletes events older than the specified retention period
        /// Implements data lifecycle management for storage optimization
        /// </summary>
        /// <param name="retentionDays">Number of days to retain events</param>
        /// <returns>Number of events deleted</returns>
        public int CleanupOldEvents(int retentionDays)
        {
            if (retentionDays <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(retentionDays), "Retention days must be positive");
            }

            try
            {
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                
                using var context = _contextFactory.CreateDbContext();
                
                // Find events to delete
                var eventsToDelete = context.Events
                    .Where(e => e.Timestamp < cutoffDate)
                    .ToList();
                
                if (eventsToDelete.Any())
                {
                    // Remove events in batches for performance
                    context.Events.RemoveRange(eventsToDelete);
                    context.SaveChanges();
                    
                    _logger.LogInformation("Cleaned up {Count} events older than {Days} days", 
                        eventsToDelete.Count, retentionDays);
                }
                
                return eventsToDelete.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old events");
                return 0;
            }
        }

        /// <summary>
        /// Forces immediate flush of pending events to database
        /// Useful for ensuring data persistence before application shutdown
        /// </summary>
        public void Flush()
        {
            lock (_lockObject)
            {
                if (_pendingEvents.Any())
                {
                    FlushPendingEvents();
                }
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Flushes pending events to the database in a batch operation
        /// Implements the Unit of Work pattern for transaction management
        /// </summary>
        private void FlushPendingEvents()
        {
            if (!_pendingEvents.Any())
            {
                return;
            }

            try
            {
                using var context = _contextFactory.CreateDbContext();
                
                // Add all pending events to context
                context.Events.AddRange(_pendingEvents);
                
                // Save changes in a single transaction
                var savedCount = context.SaveChanges();
                
                _logger.LogDebug("Flushed {Count} events to database", savedCount);
                
                // Clear pending events after successful save
                _pendingEvents.Clear();
                _lastFlush = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing {Count} events to database", _pendingEvents.Count);
                
                // Log failed events to file as backup
                foreach (var eventLog in _pendingEvents)
                {
                    LogToFile(eventLog);
                }
                
                _pendingEvents.Clear();
            }
        }

        /// <summary>
        /// Fallback method to log events to file when database is unavailable
        /// Implements the Fallback pattern for resilience
        /// </summary>
        /// <param name="eventLog">Event to log to file</param>
        private void LogToFile(EventLog eventLog)
        {
            try
            {
                var fileName = $"events_{DateTime.Now:yyyy-MM-dd}.json";
                var filePath = Path.Combine(_logDirectory, fileName);
                
                // Create a simplified version for file logging
                var fileEntry = new
                {
                    Id = eventLog.Id,
                    Timestamp = eventLog.Timestamp,
                    EventType = eventLog.EventType,
                    Description = eventLog.Description,
                    Severity = eventLog.Severity.ToString(),
                    ImagePath = eventLog.ImagePath,
                    Metadata = eventLog.Metadata,
                    AlertSent = eventLog.AlertSent,
                    Source = eventLog.Source
                };
                
                var json = JsonSerializer.Serialize(fileEntry, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                // Append to daily log file
                File.AppendAllText(filePath, json + Environment.NewLine);
                
                _logger.LogDebug("Event logged to file: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log event to file");
            }
        }

        /// <summary>
        /// Ensures database is properly initialized
        /// Creates tables and applies migrations if necessary
        /// </summary>
        public void EnsureDatabaseCreated()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                context.Database.EnsureCreated();
                
                _logger.LogInformation("Database initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing database");
                throw;
            }
        }

        #endregion

        #region Additional Interface Methods

        /// <summary>
        /// Clears all events from the database
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        public async Task ClearAllEventsAsync()
        {
            try
            {
                _logger.LogInformation("Starting to clear all events from database");
                
                using var context = _contextFactory.CreateDbContext();
                
                // Remove all events
                context.Events.RemoveRange(context.Events);
                
                // Save changes
                await context.SaveChangesAsync();
                
                _logger.LogInformation("All events cleared from database successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all events from database");
                throw;
            }
        }

        /// <summary>
        /// Deletes events before a specific date
        /// </summary>
        /// <param name="beforeDate">Delete events before this date</param>
        public async Task DeleteEventsBeforeDateAsync(DateTime beforeDate)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                
                var eventsToDelete = context.Events.Where(e => e.Timestamp < beforeDate);
                var deleteCount = await eventsToDelete.CountAsync();
                
                if (deleteCount > 0)
                {
                    context.Events.RemoveRange(eventsToDelete);
                    await context.SaveChangesAsync();
                    
                    _logger.LogInformation("Deleted {DeleteCount} events before {BeforeDate}", deleteCount, beforeDate);
                }
                else
                {
                    _logger.LogDebug("No events found to delete before {BeforeDate}", beforeDate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting events before {BeforeDate}", beforeDate);
                throw;
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Ensures all pending events are flushed before disposal
        /// Implements the Dispose pattern for proper resource cleanup
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Flush any remaining events
                Flush();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }
            
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
