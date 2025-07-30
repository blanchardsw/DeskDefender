using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using DeskDefender.Models.Alerts;
using DeskDefender.Models.Events;
using DeskDefender.Models.Settings;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Service for coordinating SMS and email alerts based on event summaries
    /// </summary>
    public class AlertService : IAlertService, IDisposable
    {
        private readonly ILogger<AlertService> _logger;
        private readonly ISettingsService _settingsService;
        private readonly IEventLogger _eventLogger;
        private readonly ISmsService _smsService;
        private readonly IEmailService _emailService;
        private Timer? _alertTimer;
        private DateTime _lastAlertTime;
        private bool _isRunning;

        public event EventHandler<AlertSummary>? AlertSent;
        public event EventHandler<string>? AlertFailed;

        public AlertService(
            ILogger<AlertService> logger,
            ISettingsService settingsService,
            IEventLogger eventLogger,
            ISmsService smsService,
            IEmailService emailService)
        {
            _logger = logger;
            _settingsService = settingsService;
            _eventLogger = eventLogger;
            _smsService = smsService;
            _emailService = emailService;
            _lastAlertTime = DateTime.Now;
        }

        /// <summary>
        /// Starts the alert service and begins monitoring for events to summarize
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                if (_isRunning)
                {
                    _logger.LogWarning("Alert service is already running");
                    return;
                }

                var alertSettings = await _settingsService.GetAlertSettingsAsync();
                
                if (!IsConfigured())
                {
                    _logger.LogWarning("Alert service not configured - neither SMS nor email alerts are enabled and configured");
                    return;
                }

                _isRunning = true;
                _lastAlertTime = DateTime.Now;

                // Set up timer for periodic alert summaries
                var intervalMs = alertSettings.SummaryIntervalMinutes * 60 * 1000;
                _alertTimer = new Timer(OnAlertTimerElapsed, null, intervalMs, intervalMs);

                _logger.LogInformation($"Alert service started with {alertSettings.SummaryIntervalMinutes} minute intervals");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start alert service");
                throw;
            }
        }

        /// <summary>
        /// Stops the alert service
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                _isRunning = false;
                _alertTimer?.Dispose();
                _alertTimer = null;

                _logger.LogInformation("Alert service stopped");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping alert service");
            }
        }

        /// <summary>
        /// Sends an immediate alert summary
        /// </summary>
        public async Task SendAlertSummaryAsync(AlertSummary summary)
        {
            try
            {
                if (!IsConfigured())
                {
                    _logger.LogWarning("Cannot send alert summary - service not configured");
                    return;
                }

                if (!summary.IsSignificant)
                {
                    _logger.LogDebug("Skipping alert summary - not significant enough");
                    return;
                }

                var alertSettings = await _settingsService.GetAlertSettingsAsync();
                var tasks = new List<Task<bool>>();

                // Send SMS alert if configured
                if (alertSettings.CanSendSms)
                {
                    tasks.Add(SendSmsAlertAsync(alertSettings.PhoneNumber!, summary));
                }

                // Send email alert if configured
                if (alertSettings.CanSendEmail)
                {
                    tasks.Add(SendEmailAlertAsync(alertSettings.EmailAddress!, summary));
                }

                // Wait for all alerts to complete
                var results = await Task.WhenAll(tasks);
                var successCount = results.Count(r => r);

                if (successCount > 0)
                {
                    _logger.LogInformation($"Alert summary sent successfully via {successCount} channel(s)");
                    AlertSent?.Invoke(this, summary);
                }
                else
                {
                    var errorMessage = "Failed to send alert summary via any configured channel";
                    _logger.LogError(errorMessage);
                    AlertFailed?.Invoke(this, errorMessage);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error sending alert summary: {ex.Message}";
                _logger.LogError(ex, errorMessage);
                AlertFailed?.Invoke(this, errorMessage);
            }
        }

        /// <summary>
        /// Generates an alert summary for the specified time period
        /// </summary>
        public async Task<AlertSummary?> GenerateAlertSummaryAsync(DateTime startTime, DateTime endTime)
        {
            try
            {
                var events = await _eventLogger.GetEventsAsync(startTime, endTime);
                if (!events.Any())
                {
                    _logger.LogDebug($"No events found for period {startTime:yyyy-MM-dd HH:mm} to {endTime:yyyy-MM-dd HH:mm}");
                    return null;
                }

                var alertSettings = await _settingsService.GetAlertSettingsAsync();
                var filteredEvents = FilterEventsBySettings(events, alertSettings);

                if (!filteredEvents.Any())
                {
                    _logger.LogDebug("No events meet alert criteria after filtering");
                    return null;
                }

                var summary = new AlertSummary
                {
                    StartTime = startTime,
                    EndTime = endTime,
                    TotalEvents = filteredEvents.Count
                };

                // Count events by severity
                foreach (var evt in filteredEvents)
                {
                    switch (evt.Severity)
                    {
                        case "Critical":
                            summary.CriticalEvents++;
                            break;
                        case "High":
                            summary.HighEvents++;
                            break;
                        case "Medium":
                        case "Warning":
                            summary.MediumEvents++;
                            break;
                        case "Low":
                            summary.LowEvents++;
                            break;
                        case "Info":
                            summary.InfoEvents++;
                            break;
                    }
                }

                // Count events by type
                foreach (var evt in filteredEvents)
                {
                    switch (evt.EventType)
                    {
                        case "Login":
                            summary.LoginEvents++;
                            break;
                        case "Input":
                            summary.InputEvents++;
                            break;
                        case "Session":
                            summary.SessionEvents++;
                            break;
                        case "Camera":
                        case "Screen Capture":
                        case "Webcam Capture":
                            summary.CameraEvents++;
                            break;
                        case "System":
                        case "BackgroundMonitoring":
                            summary.SystemEvents++;
                            break;
                    }
                }

                // Select top events (most critical first, then most recent)
                summary.TopEvents = filteredEvents
                    .OrderBy(e => GetSeverityPriority(e.Severity))
                    .ThenByDescending(e => e.Timestamp)
                    .Take(alertSettings.MaxEventsPerAlert)
                    .ToList();

                return summary.IsSignificant ? summary : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating alert summary for period {startTime:yyyy-MM-dd HH:mm} to {endTime:yyyy-MM-dd HH:mm}");
                return null;
            }
        }

        /// <summary>
        /// Sends test alerts to verify SMS and email configuration
        /// </summary>
        public async Task<bool> SendTestAlertsAsync()
        {
            try
            {
                var alertSettings = await _settingsService.GetAlertSettingsAsync();
                var results = new List<bool>();

                // Test SMS if configured
                if (alertSettings.CanSendSms)
                {
                    _logger.LogInformation($"Sending test SMS to {alertSettings.PhoneNumber}");
                    var smsResult = await _smsService.SendTestSmsAsync(alertSettings.PhoneNumber!);
                    results.Add(smsResult);
                    
                    if (smsResult)
                        _logger.LogInformation("Test SMS sent successfully");
                    else
                        _logger.LogError("Failed to send test SMS");
                }

                // Test email if configured
                if (alertSettings.CanSendEmail)
                {
                    _logger.LogInformation($"Sending test email to {alertSettings.EmailAddress}");
                    var emailResult = await _emailService.SendTestEmailAsync(alertSettings.EmailAddress!);
                    results.Add(emailResult);
                    
                    if (emailResult)
                        _logger.LogInformation("Test email sent successfully");
                    else
                        _logger.LogError("Failed to send test email");
                }

                if (!results.Any())
                {
                    _logger.LogWarning("No alert channels configured for testing");
                    return false;
                }

                var successCount = results.Count(r => r);
                _logger.LogInformation($"Test alerts completed: {successCount}/{results.Count} successful");
                
                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test alerts");
                return false;
            }
        }

        /// <summary>
        /// Checks if alert service is properly configured
        /// </summary>
        public bool IsConfigured()
        {
            try
            {
                var alertSettings = _settingsService.GetAlertSettingsAsync().Result;
                return alertSettings.CanSendSms || alertSettings.CanSendEmail;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Timer callback for periodic alert summaries
        /// </summary>
        private async void OnAlertTimerElapsed(object? state)
        {
            try
            {
                if (!_isRunning)
                    return;

                var endTime = DateTime.Now;
                var startTime = _lastAlertTime;

                _logger.LogDebug($"Generating periodic alert summary for {startTime:yyyy-MM-dd HH:mm} to {endTime:yyyy-MM-dd HH:mm}");

                var summary = await GenerateAlertSummaryAsync(startTime, endTime);
                if (summary != null)
                {
                    await SendAlertSummaryAsync(summary);
                }

                _lastAlertTime = endTime;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in periodic alert timer");
            }
        }

        /// <summary>
        /// Sends SMS alert
        /// </summary>
        private async Task<bool> SendSmsAlertAsync(string phoneNumber, AlertSummary summary)
        {
            try
            {
                _logger.LogDebug($"Sending SMS alert to {phoneNumber}");
                return await _smsService.SendAlertSummaryAsync(phoneNumber, summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending SMS alert to {phoneNumber}");
                return false;
            }
        }

        /// <summary>
        /// Sends email alert
        /// </summary>
        private async Task<bool> SendEmailAlertAsync(string emailAddress, AlertSummary summary)
        {
            try
            {
                _logger.LogDebug($"Sending email alert to {emailAddress}");
                return await _emailService.SendAlertSummaryAsync(emailAddress, summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending email alert to {emailAddress}");
                return false;
            }
        }

        /// <summary>
        /// Filters events based on alert settings
        /// </summary>
        private List<EventLog> FilterEventsBySettings(List<EventLog> events, AlertSettings settings)
        {
            var filtered = events.AsEnumerable();

            // Filter by minimum severity
            var minSeverityPriority = GetSeverityPriority(settings.MinimumAlertSeverity);
            filtered = filtered.Where(e => GetSeverityPriority(e.Severity) <= minSeverityPriority);

            // Filter system events if not included
            if (!settings.IncludeSystemEventsInAlerts)
            {
                filtered = filtered.Where(e => 
                    e.EventType != "System" && 
                    e.EventType != "BackgroundMonitoring");
            }

            return filtered.ToList();
        }

        /// <summary>
        /// Gets numeric priority for severity (lower number = higher priority)
        /// </summary>
        private int GetSeverityPriority(string severity)
        {
            return severity switch
            {
                "Critical" => 0,
                "High" => 1,
                "Medium" => 2,
                "Warning" => 2,
                "Low" => 3,
                "Info" => 4,
                _ => 5
            };
        }

        public void Dispose()
        {
            _alertTimer?.Dispose();
        }
    }
}
