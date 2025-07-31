using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using DeskDefender.Models.Alerts;
using DeskDefender.Models.Events;
using DeskDefender.Models.Settings;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Email service implementation using SMTP
    /// </summary>
    public class SmtpEmailService : IEmailService
    {
        private readonly ILogger<SmtpEmailService> _logger;
        private readonly ISettingsService _settingsService;
        private AlertSettings? _alertSettings;

        public SmtpEmailService(ILogger<SmtpEmailService> logger, ISettingsService settingsService)
        {
            _logger = logger;
            _settingsService = settingsService;
        }

        /// <summary>
        /// Initializes SMTP client with current settings
        /// </summary>
        private async Task<SmtpClient?> CreateSmtpClientAsync()
        {
            try
            {
                _alertSettings = await _settingsService.GetAlertSettingsAsync();
                
                if (!_alertSettings.CanSendEmail)
                {
                    _logger.LogWarning("Email service not properly configured");
                    return null;
                }

                var smtpClient = new SmtpClient(_alertSettings.SmtpServer)
                {
                    Port = _alertSettings.SmtpPort,
                    Credentials = new NetworkCredential(_alertSettings.SmtpUsername, _alertSettings.SmtpPassword),
                    EnableSsl = _alertSettings.SmtpUseSsl
                };

                return smtpClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create SMTP client");
                return null;
            }
        }

        /// <summary>
        /// Sends an email alert with event summary
        /// </summary>
        public async Task<bool> SendAlertSummaryAsync(string emailAddress, AlertSummary summary)
        {
            try
            {
                using var smtpClient = await CreateSmtpClientAsync();
                if (smtpClient == null)
                    return false;

                var subject = FormatEmailSubject(summary);
                var body = FormatEmailBody(summary);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_alertSettings!.SmtpUsername!, "DeskDefender Security"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(emailAddress);

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation($"Email alert sent successfully to {emailAddress}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email alert to {emailAddress}");
                return false;
            }
        }

        /// <summary>
        /// Sends a test email to verify configuration
        /// </summary>
        public async Task<bool> SendTestEmailAsync(string emailAddress)
        {
            try
            {
                using var smtpClient = await CreateSmtpClientAsync();
                if (smtpClient == null)
                    return false;

                var subject = "🛡️ DeskDefender Email Test";
                var body = FormatTestEmailBody();

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_alertSettings!.SmtpUsername!, "DeskDefender Security"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(emailAddress);

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation($"Test email sent successfully to {emailAddress}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send test email to {emailAddress}");
                return false;
            }
        }

        /// <summary>
        /// Validates email service configuration
        /// </summary>
        public bool IsConfigured()
        {
            try
            {
                var settings = _settingsService.GetAlertSettingsAsync().Result;
                return settings.CanSendEmail;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Formats email subject line based on summary severity
        /// </summary>
        private string FormatEmailSubject(AlertSummary summary)
        {
            var severityEmoji = summary.HighestSeverity switch
            {
                "Critical" => "🚨",
                "High" => "⚠️",
                "Medium" => "📋",
                "Low" => "ℹ️",
                _ => "📊"
            };

            var urgencyText = summary.HighestSeverity switch
            {
                "Critical" => "[CRITICAL]",
                "High" => "[HIGH]",
                _ => ""
            };

            return $"{severityEmoji} DeskDefender Alert {urgencyText} - {summary.TotalEvents} events ({summary.PeriodDescription})";
        }

        /// <summary>
        /// Formats HTML email body with detailed summary
        /// </summary>
        private string FormatEmailBody(AlertSummary summary)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset='utf-8'><title>DeskDefender Alert</title></head>");
            sb.AppendLine("<body style='font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5;'>");
            
            // Header
            sb.AppendLine("<div style='background-color: #2c3e50; color: white; padding: 20px; border-radius: 8px 8px 0 0;'>");
            sb.AppendLine("<h1 style='margin: 0; font-size: 24px;'>🛡️ DeskDefender Security Alert</h1>");
            sb.AppendLine("</div>");
            
            // Summary card
            var cardColor = summary.HighestSeverity switch
            {
                "Critical" => "#e74c3c",
                "High" => "#f39c12",
                "Medium" => "#f1c40f",
                "Low" => "#27ae60",
                _ => "#3498db"
            };

            sb.AppendLine($"<div style='background-color: white; padding: 20px; border-left: 5px solid {cardColor}; margin-bottom: 20px;'>");
            sb.AppendLine($"<h2 style='color: {cardColor}; margin-top: 0;'>{summary.HighestSeverity} Priority Alert</h2>");
            sb.AppendLine($"<p><strong>Time Period:</strong> {summary.StartTime:yyyy-MM-dd HH:mm} - {summary.EndTime:yyyy-MM-dd HH:mm} ({summary.PeriodDescription})</p>");
            sb.AppendLine($"<p><strong>Total Events:</strong> {summary.TotalEvents}</p>");
            sb.AppendLine("</div>");

            // Event breakdown by severity
            if (summary.CriticalEvents > 0 || summary.HighEvents > 0 || summary.MediumEvents > 0)
            {
                sb.AppendLine("<div style='background-color: white; padding: 20px; margin-bottom: 20px; border-radius: 8px;'>");
                sb.AppendLine("<h3 style='color: #2c3e50; margin-top: 0;'>📊 Events by Severity</h3>");
                sb.AppendLine("<table style='width: 100%; border-collapse: collapse;'>");
                
                if (summary.CriticalEvents > 0)
                    sb.AppendLine($"<tr><td style='padding: 8px; border-bottom: 1px solid #eee;'>🚨 Critical</td><td style='padding: 8px; border-bottom: 1px solid #eee; text-align: right; font-weight: bold; color: #e74c3c;'>{summary.CriticalEvents}</td></tr>");
                if (summary.HighEvents > 0)
                    sb.AppendLine($"<tr><td style='padding: 8px; border-bottom: 1px solid #eee;'>⚠️ High</td><td style='padding: 8px; border-bottom: 1px solid #eee; text-align: right; font-weight: bold; color: #f39c12;'>{summary.HighEvents}</td></tr>");
                if (summary.MediumEvents > 0)
                    sb.AppendLine($"<tr><td style='padding: 8px; border-bottom: 1px solid #eee;'>📋 Medium</td><td style='padding: 8px; border-bottom: 1px solid #eee; text-align: right; font-weight: bold; color: #f1c40f;'>{summary.MediumEvents}</td></tr>");
                if (summary.LowEvents > 0)
                    sb.AppendLine($"<tr><td style='padding: 8px; border-bottom: 1px solid #eee;'>ℹ️ Low</td><td style='padding: 8px; border-bottom: 1px solid #eee; text-align: right; color: #27ae60;'>{summary.LowEvents}</td></tr>");
                if (summary.InfoEvents > 0)
                    sb.AppendLine($"<tr><td style='padding: 8px;'>📄 Info</td><td style='padding: 8px; text-align: right; color: #7f8c8d;'>{summary.InfoEvents}</td></tr>");
                
                sb.AppendLine("</table>");
                sb.AppendLine("</div>");
            }

            // Event breakdown by type
            sb.AppendLine("<div style='background-color: white; padding: 20px; margin-bottom: 20px; border-radius: 8px;'>");
            sb.AppendLine("<h3 style='color: #2c3e50; margin-top: 0;'>🔍 Events by Type</h3>");
            sb.AppendLine("<table style='width: 100%; border-collapse: collapse;'>");
            
            if (summary.LoginEvents > 0)
                sb.AppendLine($"<tr><td style='padding: 8px; border-bottom: 1px solid #eee;'>🔐 Login Events</td><td style='padding: 8px; border-bottom: 1px solid #eee; text-align: right;'>{summary.LoginEvents}</td></tr>");
            if (summary.InputEvents > 0)
                sb.AppendLine($"<tr><td style='padding: 8px; border-bottom: 1px solid #eee;'>⌨️ Input Events</td><td style='padding: 8px; border-bottom: 1px solid #eee; text-align: right;'>{summary.InputEvents}</td></tr>");
            if (summary.SessionEvents > 0)
                sb.AppendLine($"<tr><td style='padding: 8px; border-bottom: 1px solid #eee;'>🔒 Session Events</td><td style='padding: 8px; border-bottom: 1px solid #eee; text-align: right;'>{summary.SessionEvents}</td></tr>");
            if (summary.CameraEvents > 0)
                sb.AppendLine($"<tr><td style='padding: 8px; border-bottom: 1px solid #eee;'>📷 Camera Events</td><td style='padding: 8px; border-bottom: 1px solid #eee; text-align: right;'>{summary.CameraEvents}</td></tr>");
            if (summary.SystemEvents > 0)
                sb.AppendLine($"<tr><td style='padding: 8px;'>⚙️ System Events</td><td style='padding: 8px; text-align: right;'>{summary.SystemEvents}</td></tr>");
            
            sb.AppendLine("</table>");
            sb.AppendLine("</div>");

            // Top events
            if (summary.TopEvents.Count > 0)
            {
                sb.AppendLine("<div style='background-color: white; padding: 20px; margin-bottom: 20px; border-radius: 8px;'>");
                sb.AppendLine("<h3 style='color: #2c3e50; margin-top: 0;'>🔍 Key Events</h3>");
                
                foreach (var evt in summary.TopEvents.Take(10))
                {
                    var eventColor = evt.Severity switch
                    {
                        EventSeverity.Critical => "#e74c3c",
                        EventSeverity.High => "#f39c12",
                        EventSeverity.Medium => "#f1c40f",
                        _ => "#3498db"
                    };

                    sb.AppendLine($"<div style='border-left: 3px solid {eventColor}; padding: 10px; margin-bottom: 10px; background-color: #f8f9fa;'>");
                    sb.AppendLine($"<strong style='color: {eventColor};'>{evt.EventType}</strong> - {evt.Severity}");
                    sb.AppendLine($"<br><span style='color: #7f8c8d; font-size: 12px;'>{evt.Timestamp:yyyy-MM-dd HH:mm:ss}</span>");
                    sb.AppendLine($"<br>{evt.Description}");
                    sb.AppendLine("</div>");
                }
                
                sb.AppendLine("</div>");
            }

            // Footer
            sb.AppendLine("<div style='background-color: #34495e; color: #bdc3c7; padding: 15px; border-radius: 0 0 8px 8px; text-align: center; font-size: 12px;'>");
            sb.AppendLine("<p style='margin: 0;'>This alert was generated by DeskDefender Security Monitoring</p>");
            sb.AppendLine($"<p style='margin: 5px 0 0 0;'>Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            sb.AppendLine("</div>");
            
            sb.AppendLine("</body></html>");
            
            return sb.ToString();
        }

        /// <summary>
        /// Formats test email body
        /// </summary>
        private string FormatTestEmailBody()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset='utf-8'><title>DeskDefender Test</title></head>");
            sb.AppendLine("<body style='font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5;'>");
            
            sb.AppendLine("<div style='background-color: #27ae60; color: white; padding: 20px; border-radius: 8px; text-align: center;'>");
            sb.AppendLine("<h1 style='margin: 0;'>✅ DeskDefender Email Test</h1>");
            sb.AppendLine("<p style='margin: 10px 0 0 0;'>Your email alert configuration is working correctly!</p>");
            sb.AppendLine("</div>");
            
            sb.AppendLine("<div style='background-color: white; padding: 20px; margin-top: 20px; border-radius: 8px;'>");
            sb.AppendLine("<h3>Configuration Test Results:</h3>");
            sb.AppendLine("<p>✅ SMTP connection successful</p>");
            sb.AppendLine("<p>✅ Authentication successful</p>");
            sb.AppendLine("<p>✅ Email delivery successful</p>");
            sb.AppendLine($"<p><strong>Test completed at:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            sb.AppendLine("</div>");
            
            sb.AppendLine("</body></html>");
            
            return sb.ToString();
        }
    }
}
