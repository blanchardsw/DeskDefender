using System.ComponentModel.DataAnnotations;

namespace DeskDefender.Models.Settings
{
    /// <summary>
    /// Configuration settings for SMS and email alerts
    /// </summary>
    public class AlertSettings
    {
        /// <summary>
        /// Phone number for SMS alerts (E.164 format: +1234567890)
        /// </summary>
        [Phone]
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Email address for email alerts
        /// </summary>
        [EmailAddress]
        public string? EmailAddress { get; set; }

        /// <summary>
        /// Whether SMS alerts are enabled
        /// </summary>
        public bool SmsAlertsEnabled { get; set; } = false;

        /// <summary>
        /// Whether email alerts are enabled
        /// </summary>
        public bool EmailAlertsEnabled { get; set; } = false;

        /// <summary>
        /// Summary interval in minutes (separate from monitoring interval)
        /// Default: 60 minutes
        /// </summary>
        public int SummaryIntervalMinutes { get; set; } = 60;

        /// <summary>
        /// Minimum severity level for alerts (Critical, High, Medium, Low, Info)
        /// </summary>
        public string MinimumAlertSeverity { get; set; } = "Medium";

        /// <summary>
        /// Whether to include system events in alert summaries
        /// </summary>
        public bool IncludeSystemEventsInAlerts { get; set; } = false;

        /// <summary>
        /// Maximum number of events to include in a single alert summary
        /// </summary>
        public int MaxEventsPerAlert { get; set; } = 20;

        /// <summary>
        /// SMTP server for email alerts (if using custom email provider)
        /// </summary>
        public string? SmtpServer { get; set; }

        /// <summary>
        /// SMTP port for email alerts
        /// </summary>
        public int SmtpPort { get; set; } = 587;

        /// <summary>
        /// SMTP username for email alerts
        /// </summary>
        public string? SmtpUsername { get; set; }

        /// <summary>
        /// SMTP password for email alerts (should be encrypted in storage)
        /// </summary>
        public string? SmtpPassword { get; set; }

        /// <summary>
        /// Whether to use SSL for SMTP connection
        /// </summary>
        public bool SmtpUseSsl { get; set; } = true;

        /// <summary>
        /// Twilio Account SID for SMS alerts
        /// </summary>
        public string? TwilioAccountSid { get; set; }

        /// <summary>
        /// Twilio Auth Token for SMS alerts (should be encrypted in storage)
        /// </summary>
        public string? TwilioAuthToken { get; set; }

        /// <summary>
        /// Twilio phone number for sending SMS alerts
        /// </summary>
        public string? TwilioPhoneNumber { get; set; }

        /// <summary>
        /// Validates if SMS alerts can be sent with current configuration
        /// </summary>
        public bool CanSendSms => 
            SmsAlertsEnabled && 
            !string.IsNullOrWhiteSpace(PhoneNumber) &&
            !string.IsNullOrWhiteSpace(TwilioAccountSid) &&
            !string.IsNullOrWhiteSpace(TwilioAuthToken) &&
            !string.IsNullOrWhiteSpace(TwilioPhoneNumber);

        /// <summary>
        /// Validates if email alerts can be sent with current configuration
        /// </summary>
        public bool CanSendEmail => 
            EmailAlertsEnabled && 
            !string.IsNullOrWhiteSpace(EmailAddress) &&
            !string.IsNullOrWhiteSpace(SmtpServer) &&
            !string.IsNullOrWhiteSpace(SmtpUsername) &&
            !string.IsNullOrWhiteSpace(SmtpPassword);
    }
}
