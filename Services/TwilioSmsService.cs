using System;
using System.Text;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using DeskDefender.Models.Alerts;
using DeskDefender.Models.Settings;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace DeskDefender.Services
{
    /// <summary>
    /// SMS service implementation using Twilio
    /// </summary>
    public class TwilioSmsService : ISmsService
    {
        private readonly ILogger<TwilioSmsService> _logger;
        private readonly ISettingsService _settingsService;
        private AlertSettings? _alertSettings;

        public TwilioSmsService(ILogger<TwilioSmsService> logger, ISettingsService settingsService)
        {
            _logger = logger;
            _settingsService = settingsService;
        }

        /// <summary>
        /// Initializes Twilio client with current settings
        /// </summary>
        private async Task<bool> InitializeTwilioAsync()
        {
            try
            {
                _alertSettings = await _settingsService.GetAlertSettingsAsync();
                
                if (!_alertSettings.CanSendSms)
                {
                    _logger.LogWarning("SMS service not properly configured");
                    return false;
                }

                TwilioClient.Init(_alertSettings.TwilioAccountSid, _alertSettings.TwilioAuthToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Twilio client");
                return false;
            }
        }

        /// <summary>
        /// Sends an SMS alert with event summary
        /// </summary>
        public async Task<bool> SendAlertSummaryAsync(string phoneNumber, AlertSummary summary)
        {
            try
            {
                if (!await InitializeTwilioAsync())
                    return false;

                var message = FormatAlertSummaryMessage(summary);
                
                var messageResource = await MessageResource.CreateAsync(
                    body: message,
                    from: new PhoneNumber(_alertSettings!.TwilioPhoneNumber),
                    to: new PhoneNumber(phoneNumber)
                );

                _logger.LogInformation($"SMS alert sent successfully. SID: {messageResource.Sid}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send SMS alert to {phoneNumber}");
                return false;
            }
        }

        /// <summary>
        /// Sends a test SMS to verify configuration
        /// </summary>
        public async Task<bool> SendTestSmsAsync(string phoneNumber)
        {
            try
            {
                if (!await InitializeTwilioAsync())
                    return false;

                var testMessage = "🛡️ DeskDefender SMS Test\n\nThis is a test message to verify your SMS alert configuration is working correctly.\n\nTime: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                var messageResource = await MessageResource.CreateAsync(
                    body: testMessage,
                    from: new PhoneNumber(_alertSettings!.TwilioPhoneNumber),
                    to: new PhoneNumber(phoneNumber)
                );

                _logger.LogInformation($"Test SMS sent successfully. SID: {messageResource.Sid}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send test SMS to {phoneNumber}");
                return false;
            }
        }

        /// <summary>
        /// Validates SMS service configuration
        /// </summary>
        public bool IsConfigured()
        {
            try
            {
                var settings = _settingsService.GetAlertSettingsAsync().Result;
                return settings.CanSendSms;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Formats alert summary into SMS message
        /// </summary>
        private string FormatAlertSummaryMessage(AlertSummary summary)
        {
            var sb = new StringBuilder();
            
            // Header with emoji and severity indicator
            var severityEmoji = summary.HighestSeverity switch
            {
                "Critical" => "🚨",
                "High" => "⚠️",
                "Medium" => "📋",
                "Low" => "ℹ️",
                _ => "📊"
            };

            sb.AppendLine($"{severityEmoji} DeskDefender Alert Summary");
            sb.AppendLine($"Period: {summary.PeriodDescription}");
            sb.AppendLine($"Time: {summary.StartTime:MM/dd HH:mm} - {summary.EndTime:HH:mm}");
            sb.AppendLine();

            // Event counts by severity
            if (summary.CriticalEvents > 0)
                sb.AppendLine($"🚨 Critical: {summary.CriticalEvents}");
            if (summary.HighEvents > 0)
                sb.AppendLine($"⚠️ High: {summary.HighEvents}");
            if (summary.MediumEvents > 0)
                sb.AppendLine($"📋 Medium: {summary.MediumEvents}");
            if (summary.LowEvents > 0)
                sb.AppendLine($"ℹ️ Low: {summary.LowEvents}");

            sb.AppendLine();

            // Event counts by type
            if (summary.LoginEvents > 0)
                sb.AppendLine($"🔐 Logins: {summary.LoginEvents}");
            if (summary.InputEvents > 0)
                sb.AppendLine($"⌨️ Input: {summary.InputEvents}");
            if (summary.SessionEvents > 0)
                sb.AppendLine($"🔒 Sessions: {summary.SessionEvents}");
            if (summary.CameraEvents > 0)
                sb.AppendLine($"📷 Camera: {summary.CameraEvents}");

            // Top events (most critical)
            if (summary.TopEvents.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("🔍 Key Events:");
                foreach (var evt in summary.TopEvents.Take(3)) // Limit to 3 for SMS
                {
                    sb.AppendLine($"• {evt.EventType}: {evt.Description}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"Total Events: {summary.TotalEvents}");

            return sb.ToString();
        }
    }
}
