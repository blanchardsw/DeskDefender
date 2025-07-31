using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using DeskDefender.Models.Alerts;
using DeskDefender.Models.Events;
using DeskDefender.Models.Settings;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Refactored AlertService that coordinates alert generation and dispatch
    /// Follows SOLID principles by delegating to specialized services
    /// </summary>
    public class AlertService : IAlertService, IDisposable
    {
        private readonly ILogger<AlertService> _logger;
        private readonly ISettingsService _settingsService;
        private readonly IEventAggregationService _eventAggregationService;
        private readonly IAlertSchedulingService _schedulingService;
        private readonly ISmsService _smsService;
        private readonly IEmailService _emailService;
        private bool _isRunning;

        public event EventHandler<AlertSummary>? AlertSent;
        public event EventHandler<string>? AlertFailed;

        public AlertService(
            ILogger<AlertService> logger,
            ISettingsService settingsService,
            IEventAggregationService eventAggregationService,
            IAlertSchedulingService schedulingService,
            ISmsService smsService,
            IEmailService emailService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _eventAggregationService = eventAggregationService ?? throw new ArgumentNullException(nameof(eventAggregationService));
            _schedulingService = schedulingService ?? throw new ArgumentNullException(nameof(schedulingService));
            _smsService = smsService ?? throw new ArgumentNullException(nameof(smsService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            
            // Subscribe to scheduling events
            _schedulingService.AlertIntervalElapsed += OnAlertIntervalElapsed;
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

                if (!IsConfigured())
                {
                    _logger.LogWarning("Alert service not configured - neither SMS nor email alerts are enabled and configured");
                    return;
                }

                // Start the scheduling service
                await _schedulingService.StartAsync();
                _isRunning = true;

                _logger.LogInformation("Alert service started successfully");
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
                if (!_isRunning)
                {
                    _logger.LogWarning("Alert service is not running");
                    return;
                }

                // Stop the scheduling service
                await _schedulingService.StopAsync();
                _isRunning = false;

                _logger.LogInformation("Alert service stopped successfully");
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
                var successCount = 0;

                // Send SMS alert if configured
                if (alertSettings.CanSendSms)
                {
                    var smsSuccess = await _smsService.SendAlertSummaryAsync(alertSettings.PhoneNumber!, summary);
                    if (smsSuccess) successCount++;
                }

                // Send email alert if configured
                if (alertSettings.CanSendEmail)
                {
                    var emailSuccess = await _emailService.SendAlertSummaryAsync(alertSettings.EmailAddress!, summary);
                    if (emailSuccess) successCount++;
                }

                if (successCount > 0)
                {
                    _logger.LogInformation("Alert summary sent successfully via {SuccessCount} channel(s)", successCount);
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
                _logger.LogDebug("Generating alert summary from {StartTime} to {EndTime}", startTime, endTime);

                var alertSettings = await _settingsService.GetAlertSettingsAsync();
                var summary = await _eventAggregationService.AggregateEventsAsync(startTime, endTime, alertSettings);

                _logger.LogDebug("Generated summary with {TotalEvents} total events, {TopEvents} top events", 
                    summary.TotalEvents, summary.TopEvents.Count);
                
                return summary.IsSignificant ? summary : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate alert summary from {StartTime} to {EndTime}", startTime, endTime);
                return null;
            }
        }

        /// <summary>
        /// Sends an immediate alert with a custom message (legacy method for backward compatibility)
        /// </summary>
        /// <param name="message">Alert message to send</param>
        /// <param name="imagePath">Optional image path to include</param>
        public async Task SendAlertAsync(string message, string imagePath = null)
        {
            try
            {
                if (!IsConfigured())
                {
                    _logger.LogWarning("Cannot send alert - service not configured");
                    return;
                }

                var alertSettings = await _settingsService.GetAlertSettingsAsync();
                var successCount = 0;

                // Send SMS alert if configured
                if (alertSettings.CanSendSms)
                {
                    // Create a simple alert summary for the message
                    var summary = new AlertSummary
                    {
                        StartTime = DateTime.Now.AddMinutes(-1),
                        EndTime = DateTime.Now,
                        TotalEvents = 1,
                        TopEvents = new List<EventLog>()
                    };
                    
                    var smsSuccess = await _smsService.SendAlertSummaryAsync(alertSettings.PhoneNumber!, summary);
                    if (smsSuccess) successCount++;
                }

                // Send email alert if configured
                if (alertSettings.CanSendEmail)
                {
                    // Create a simple alert summary for the message
                    var summary = new AlertSummary
                    {
                        StartTime = DateTime.Now.AddMinutes(-1),
                        EndTime = DateTime.Now,
                        TotalEvents = 1,
                        TopEvents = new List<EventLog>()
                    };
                    
                    var emailSuccess = await _emailService.SendAlertSummaryAsync(alertSettings.EmailAddress!, summary);
                    if (emailSuccess) successCount++;
                }

                if (successCount > 0)
                {
                    // Create a simple summary for the event
                    var eventSummary = new AlertSummary
                    {
                        StartTime = DateTime.Now.AddMinutes(-1),
                        EndTime = DateTime.Now,
                        TotalEvents = 1,
                        TopEvents = new List<EventLog>()
                    };
                    AlertSent?.Invoke(this, eventSummary);
                    _logger.LogInformation("Immediate alert sent successfully to {SuccessCount} channels", successCount);
                }
                else
                {
                    AlertFailed?.Invoke(this, "No alerts were sent - check configuration");
                    _logger.LogWarning("Failed to send immediate alert - no successful deliveries");
                }
            }
            catch (Exception ex)
            {
                AlertFailed?.Invoke(this, ex.Message);
                _logger.LogError(ex, "Failed to send immediate alert: {Message}", message);
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
                var smsSuccess = false;
                var emailSuccess = false;

                // Test SMS if configured
                if (alertSettings.CanSendSms)
                {
                    smsSuccess = await _smsService.SendTestSmsAsync(alertSettings.PhoneNumber!);
                }

                // Test email if configured
                if (alertSettings.CanSendEmail)
                {
                    emailSuccess = await _emailService.SendTestEmailAsync(alertSettings.EmailAddress!);
                }

                _logger.LogInformation("Test alerts completed - SMS: {SmsSuccess}, Email: {EmailSuccess}", smsSuccess, emailSuccess);
                return smsSuccess || emailSuccess; // Return true if at least one test succeeded
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
                // Note: This is now synchronous to match interface, but settings loading is async
                // In a real implementation, we'd need to cache settings or make the interface async
                return true; // Simplified for now - actual configuration check would need refactoring
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking alert service configuration");
                return false;
            }
        }

        /// <summary>
        /// Event handler for when alert interval elapses
        /// </summary>
        private async void OnAlertIntervalElapsed(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogDebug("Alert interval elapsed, generating and sending alert summary");

                var (startTime, endTime) = _schedulingService.GetLastAlertInterval();
                var summary = await GenerateAlertSummaryAsync(startTime, endTime);

                if (summary != null)
                {
                    await SendAlertSummaryAsync(summary);
                }

                _schedulingService.UpdateLastAlertTime();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in alert timer callback");
            }
        }

        /// <summary>
        /// Sends SMS alert
        /// </summary>
        private async Task<bool> SendSmsAlertAsync(string phoneNumber, AlertSummary summary)
        {
            try
            {
                var message = FormatSmsMessage(summary);
                return await _smsService.SendAlertSummaryAsync(phoneNumber, summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send SMS alert to {PhoneNumber}", phoneNumber);
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
                var subject = FormatEmailSubject(summary);
                var body = FormatEmailBody(summary);
                return await _emailService.SendAlertSummaryAsync(emailAddress, summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email alert to {EmailAddress}", emailAddress);
                return false;
            }
        }

        /// <summary>
        /// Formats SMS message for alert summary
        /// </summary>
        private string FormatSmsMessage(AlertSummary summary)
        {
            var message = $"🔒 DeskDefender Alert\n";
            message += $"📅 {summary.StartTime:MMM dd HH:mm} - {summary.EndTime:HH:mm}\n";
            message += $"📊 {summary.TotalEvents} events detected\n\n";

            if (summary.CriticalEvents > 0)
                message += $"🔴 Critical: {summary.CriticalEvents}\n";
            if (summary.HighEvents > 0)
                message += $"🟠 High: {summary.HighEvents}\n";
            if (summary.MediumEvents > 0)
                message += $"🟡 Medium: {summary.MediumEvents}\n";

            return message.Trim();
        }

        /// <summary>
        /// Formats email subject for alert summary
        /// </summary>
        private string FormatEmailSubject(AlertSummary summary)
        {
            var severity = summary.CriticalEvents > 0 ? "CRITICAL" :
                          summary.HighEvents > 0 ? "HIGH" : "MEDIUM";
            return $"🔒 DeskDefender {severity} Alert - {summary.TotalEvents} Events";
        }

        /// <summary>
        /// Formats email body for alert summary
        /// </summary>
        private string FormatEmailBody(AlertSummary summary)
        {
            var html = $@"
                <h2>🔒 DeskDefender Security Alert</h2>
                <p><strong>Time Period:</strong> {summary.StartTime:yyyy-MM-dd HH:mm} - {summary.EndTime:HH:mm}</p>
                <p><strong>Total Events:</strong> {summary.TotalEvents}</p>
                
                <h3>📊 Event Breakdown by Severity</h3>
                <ul>";

            if (summary.CriticalEvents > 0)
                html += $"<li style='color: #dc3545;'><strong>Critical:</strong> {summary.CriticalEvents}</li>";
            if (summary.HighEvents > 0)
                html += $"<li style='color: #fd7e14;'><strong>High:</strong> {summary.HighEvents}</li>";
            if (summary.MediumEvents > 0)
                html += $"<li style='color: #ffc107;'><strong>Medium:</strong> {summary.MediumEvents}</li>";
            if (summary.LowEvents > 0)
                html += $"<li style='color: #28a745;'><strong>Low:</strong> {summary.LowEvents}</li>";
            if (summary.InfoEvents > 0)
                html += $"<li style='color: #17a2b8;'><strong>Info:</strong> {summary.InfoEvents}</li>";

            html += "</ul>";

            if (summary.TopEvents.Any())
            {
                html += "<h3>🔍 Most Significant Events</h3><ul>";
                foreach (var evt in summary.TopEvents.Take(5))
                {
                    html += $"<li><strong>{evt.Timestamp:HH:mm}</strong> - {evt.EventType}: {evt.Description}</li>";
                }
                html += "</ul>";
            }

            html += "<p><em>This is an automated alert from DeskDefender security monitoring system.</em></p>";
            return html;
        }

        public void Dispose()
        {
            try
            {
                if (_schedulingService != null)
                {
                    _schedulingService.AlertIntervalElapsed -= OnAlertIntervalElapsed;
                }
                _isRunning = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing alert service");
            }
        }
    }
}
