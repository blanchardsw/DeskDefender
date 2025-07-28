using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DeskDefender.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Service for maintaining database health through automatic purging and size management
    /// </summary>
    public class DatabaseMaintenanceService
    {
        private readonly ILogger<DatabaseMaintenanceService> _logger;
        private readonly IEventLogger _eventLogger;
        private readonly SettingsService _settingsService;
        private readonly string _databasePath;

        public DatabaseMaintenanceService(
            ILogger<DatabaseMaintenanceService> logger, 
            IEventLogger eventLogger,
            SettingsService settingsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            
            // Get database path (assuming SQLite database in AppData)
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "DeskDefender");
            _databasePath = Path.Combine(appFolder, "events.db");
        }

        /// <summary>
        /// Perform database maintenance including age-based and size-based purging
        /// </summary>
        public async Task PerformMaintenanceAsync()
        {
            try
            {
                _logger.LogInformation("Starting database maintenance");

                // Check database size first
                await CheckDatabaseSizeAsync();

                // Perform age-based purging
                await PurgeOldEventsAsync();

                _logger.LogInformation("Database maintenance completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database maintenance");
            }
        }

        /// <summary>
        /// Check database size and warn user if it exceeds configured limit
        /// </summary>
        private async Task CheckDatabaseSizeAsync()
        {
            try
            {
                if (!File.Exists(_databasePath))
                {
                    _logger.LogDebug("Database file not found, skipping size check");
                    return;
                }

                var fileInfo = new FileInfo(_databasePath);
                var sizeInMB = fileInfo.Length / (1024.0 * 1024.0);
                var maxSizeMB = _settingsService.Settings.MaxDatabaseSizeMB;

                _logger.LogDebug("Database size: {SizeMB:F2} MB, Max allowed: {MaxSizeMB} MB", sizeInMB, maxSizeMB);

                if (sizeInMB > maxSizeMB)
                {
                    _logger.LogWarning("Database size ({SizeMB:F2} MB) exceeds maximum ({MaxSizeMB} MB)", sizeInMB, maxSizeMB);
                    
                    // Show warning to user on UI thread
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var result = System.Windows.MessageBox.Show(
                            $"Database size ({sizeInMB:F1} MB) exceeds the configured limit ({maxSizeMB} MB).\n\n" +
                            "Would you like to purge old events to reduce database size?\n\n" +
                            "This will permanently delete events older than the configured retention period.",
                            "Database Size Warning",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Warning);

                        if (result == System.Windows.MessageBoxResult.Yes)
                        {
                            // Perform aggressive purging
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await PurgeOldEventsAsync(aggressive: true);
                                    
                                    // Show completion message
                                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                    {
                                        System.Windows.MessageBox.Show(
                                            "Database purging completed successfully.",
                                            "Purge Complete",
                                            System.Windows.MessageBoxButton.OK,
                                            System.Windows.MessageBoxImage.Information);
                                    });
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error during aggressive database purging");
                                    
                                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                    {
                                        System.Windows.MessageBox.Show(
                                            $"Error during database purging: {ex.Message}",
                                            "Purge Error",
                                            System.Windows.MessageBoxButton.OK,
                                            System.Windows.MessageBoxImage.Error);
                                    });
                                }
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking database size");
            }
        }

        /// <summary>
        /// Purge events older than the configured retention period
        /// </summary>
        /// <param name="aggressive">If true, use more aggressive purging criteria</param>
        private async Task PurgeOldEventsAsync(bool aggressive = false)
        {
            try
            {
                var retentionDays = _settingsService.Settings.EventRetentionDays;
                
                // Use more aggressive retention for size-based purging
                if (aggressive)
                {
                    retentionDays = Math.Min(retentionDays, 7); // Keep only last 7 days when aggressive
                }

                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
                
                _logger.LogInformation("Purging events older than {CutoffDate} (retention: {RetentionDays} days, aggressive: {Aggressive})", 
                    cutoffDate, retentionDays, aggressive);

                // Get events to be purged for logging
                var eventsToDelete = await _eventLogger.GetEventsAsync(DateTime.MinValue, cutoffDate);
                var deleteCount = eventsToDelete.Count();

                if (deleteCount == 0)
                {
                    _logger.LogInformation("No events found for purging");
                    return;
                }

                // Perform the purge
                await _eventLogger.DeleteEventsBeforeDateAsync(cutoffDate);
                
                _logger.LogInformation("Successfully purged {DeleteCount} events older than {CutoffDate}", deleteCount, cutoffDate);

                // Log the purge operation itself
                await _eventLogger.LogEventAsync(new Models.Events.EventLog
                {
                    EventType = "DatabaseMaintenance",
                    Description = $"Purged {deleteCount} events older than {retentionDays} days" + (aggressive ? " (aggressive mode)" : ""),
                    Timestamp = DateTime.UtcNow,
                    Severity = Models.Events.EventSeverity.Info,
                    IsAlert = false
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging old events");
                throw;
            }
        }

        /// <summary>
        /// Get database statistics for monitoring
        /// </summary>
        public async Task<DatabaseStatistics> GetDatabaseStatisticsAsync()
        {
            try
            {
                var stats = new DatabaseStatistics();

                // Get file size
                if (File.Exists(_databasePath))
                {
                    var fileInfo = new FileInfo(_databasePath);
                    stats.SizeInMB = fileInfo.Length / (1024.0 * 1024.0);
                    stats.LastModified = fileInfo.LastWriteTime;
                }

                // Get event counts
                var allEvents = await _eventLogger.GetEventsAsync(DateTime.MinValue, DateTime.MaxValue);
                stats.TotalEvents = allEvents.Count();
                
                if (allEvents.Any())
                {
                    stats.OldestEvent = allEvents.Min(e => e.Timestamp);
                    stats.NewestEvent = allEvents.Max(e => e.Timestamp);
                    
                    var cutoffDate = DateTime.UtcNow.AddDays(-_settingsService.Settings.EventRetentionDays);
                    stats.EventsEligibleForPurge = allEvents.Count(e => e.Timestamp < cutoffDate);
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database statistics");
                throw;
            }
        }

        /// <summary>
        /// Manually trigger database maintenance
        /// </summary>
        public async Task<MaintenanceResult> TriggerMaintenanceAsync()
        {
            try
            {
                var statsBefore = await GetDatabaseStatisticsAsync();
                
                await PerformMaintenanceAsync();
                
                var statsAfter = await GetDatabaseStatisticsAsync();
                
                return new MaintenanceResult
                {
                    Success = true,
                    EventsPurged = statsBefore.TotalEvents - statsAfter.TotalEvents,
                    SizeReductionMB = statsBefore.SizeInMB - statsAfter.SizeInMB,
                    StatsBefore = statsBefore,
                    StatsAfter = statsAfter
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual maintenance trigger");
                return new MaintenanceResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }

    /// <summary>
    /// Database statistics data model
    /// </summary>
    public class DatabaseStatistics
    {
        public double SizeInMB { get; set; }
        public int TotalEvents { get; set; }
        public int EventsEligibleForPurge { get; set; }
        public DateTime? OldestEvent { get; set; }
        public DateTime? NewestEvent { get; set; }
        public DateTime LastModified { get; set; }
    }

    /// <summary>
    /// Maintenance operation result
    /// </summary>
    public class MaintenanceResult
    {
        public bool Success { get; set; }
        public int EventsPurged { get; set; }
        public double SizeReductionMB { get; set; }
        public string? ErrorMessage { get; set; }
        public DatabaseStatistics? StatsBefore { get; set; }
        public DatabaseStatistics? StatsAfter { get; set; }
    }
}
