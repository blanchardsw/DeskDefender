using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Service for exporting event logs to various file formats
    /// </summary>
    public class LogExportService
    {
        private readonly ILogger<LogExportService> _logger;
        private readonly IEventLogger _eventLogger;

        public LogExportService(ILogger<LogExportService> logger, IEventLogger eventLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
        }

        /// <summary>
        /// Export logs to file based on file extension
        /// </summary>
        /// <param name="filePath">Path to export file</param>
        public async Task ExportLogsAsync(string filePath)
        {
            try
            {
                _logger.LogInformation("Starting log export to {FilePath}", filePath);

                // Get all events from database
                var events = await _eventLogger.GetEventsAsync(DateTime.MinValue, DateTime.MaxValue);
                
                if (!events.Any())
                {
                    _logger.LogWarning("No events found to export");
                    throw new InvalidOperationException("No events found to export");
                }

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                switch (extension)
                {
                    case ".csv":
                        await ExportToCsvAsync(filePath, events);
                        break;
                    case ".json":
                        await ExportToJsonAsync(filePath, events);
                        break;
                    case ".txt":
                        await ExportToTextAsync(filePath, events);
                        break;
                    default:
                        throw new ArgumentException($"Unsupported file format: {extension}");
                }

                _logger.LogInformation("Log export completed successfully to {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export logs to {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Export logs to CSV format
        /// </summary>
        private async Task ExportToCsvAsync(string filePath, IEnumerable<EventLog> events)
        {
            var csv = new StringBuilder();
            
            // CSV Header
            csv.AppendLine("Timestamp,EventType,Severity,Description,IsAlert,Details");
            
            // CSV Data
            foreach (var eventLog in events.OrderBy(e => e.Timestamp))
            {
                var description = EscapeCsvField(eventLog.Description);
                var details = EscapeCsvField(eventLog.Details ?? "");
                
                csv.AppendLine($"{eventLog.Timestamp:yyyy-MM-dd HH:mm:ss},{eventLog.EventType},{eventLog.Severity},{description},{eventLog.IsAlert},{details}");
            }
            
            await File.WriteAllTextAsync(filePath, csv.ToString(), Encoding.UTF8);
            _logger.LogDebug("Exported {Count} events to CSV format", events.Count());
        }

        /// <summary>
        /// Export logs to JSON format
        /// </summary>
        private async Task ExportToJsonAsync(string filePath, IEnumerable<EventLog> events)
        {
            var exportData = new
            {
                ExportTimestamp = DateTime.UtcNow,
                TotalEvents = events.Count(),
                Events = events.OrderBy(e => e.Timestamp).Select(e => new
                {
                    e.Id,
                    e.Timestamp,
                    e.EventType,
                    e.Severity,
                    e.Description,
                    e.IsAlert,
                    e.AlertSent,
                    e.Details
                })
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(exportData, options);
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
            _logger.LogDebug("Exported {Count} events to JSON format", events.Count());
        }

        /// <summary>
        /// Export logs to plain text format
        /// </summary>
        private async Task ExportToTextAsync(string filePath, IEnumerable<EventLog> events)
        {
            var text = new StringBuilder();
            
            text.AppendLine("DeskDefender Event Log Export");
            text.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            text.AppendLine($"Total Events: {events.Count()}");
            text.AppendLine(new string('=', 80));
            text.AppendLine();

            foreach (var eventLog in events.OrderBy(e => e.Timestamp))
            {
                text.AppendLine($"[{eventLog.Timestamp:yyyy-MM-dd HH:mm:ss}] {eventLog.Severity} - {eventLog.EventType}");
                text.AppendLine($"Description: {eventLog.Description}");
                
                if (eventLog.IsAlert)
                {
                    text.AppendLine($"Alert Status: {(eventLog.AlertSent ? "Sent" : "Pending")}");
                }
                
                if (!string.IsNullOrEmpty(eventLog.Details))
                {
                    text.AppendLine($"Details: {eventLog.Details}");
                }
                
                text.AppendLine(new string('-', 40));
                text.AppendLine();
            }

            await File.WriteAllTextAsync(filePath, text.ToString(), Encoding.UTF8);
            _logger.LogDebug("Exported {Count} events to text format", events.Count());
        }

        /// <summary>
        /// Escape CSV field to handle commas, quotes, and newlines
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            // If field contains comma, quote, or newline, wrap in quotes and escape internal quotes
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }

            return field;
        }

        /// <summary>
        /// Get export statistics
        /// </summary>
        public async Task<ExportStatistics> GetExportStatisticsAsync()
        {
            try
            {
                var events = await _eventLogger.GetEventsAsync(DateTime.MinValue, DateTime.MaxValue);
                
                return new ExportStatistics
                {
                    TotalEvents = events.Count(),
                    EventsByType = events.GroupBy(e => e.EventType)
                                        .ToDictionary(g => g.Key, g => g.Count()),
                    EventsBySeverity = events.GroupBy(e => e.Severity)
                                           .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                    DateRange = events.Any() ? 
                        new DateRange 
                        { 
                            Start = events.Min(e => e.Timestamp), 
                            End = events.Max(e => e.Timestamp) 
                        } : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get export statistics");
                throw;
            }
        }
    }

    /// <summary>
    /// Export statistics data model
    /// </summary>
    public class ExportStatistics
    {
        public int TotalEvents { get; set; }
        public Dictionary<string, int> EventsByType { get; set; } = new();
        public Dictionary<string, int> EventsBySeverity { get; set; } = new();
        public DateRange DateRange { get; set; }
    }

    /// <summary>
    /// Date range data model
    /// </summary>
    public class DateRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}
