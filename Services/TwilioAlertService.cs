using System;
using System.IO;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using DeskDefender.Models.Configuration;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace DeskDefender.Services
{
    /// <summary>
    /// Twilio-based SMS alert service implementation
    /// Implements the Strategy pattern for different alert delivery methods
    /// Uses the Template Method pattern for message formatting
    /// Provides both synchronous and asynchronous alert delivery
    /// </summary>
    public class TwilioAlertService : IAlertService
    {
        #region Private Fields

        private readonly ILogger<TwilioAlertService> _logger;
        private readonly AppSettings _settings;
        private bool _isInitialized = false;
        private readonly object _lockObject = new object();

        // Rate limiting to prevent SMS spam
        private DateTime _lastAlertTime = DateTime.MinValue;
        private readonly TimeSpan _minimumAlertInterval = TimeSpan.FromMinutes(1);
        private int _alertCount = 0;
        private readonly int _maxAlertsPerHour = 10;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the TwilioAlertService
        /// Uses dependency injection for configuration and logging
        /// </summary>
        /// <param name="settings">Application settings containing Twilio configuration</param>
        /// <param name="logger">Logger instance for diagnostic information</param>
        public TwilioAlertService(AppSettings settings, ILogger<TwilioAlertService> logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            InitializeTwilio();
        }

        #endregion

        #region IAlertService Implementation

        /// <summary>
        /// Gets the display name of the alert service
        /// </summary>
        public string ServiceName => "Twilio SMS Alert Service";

        /// <summary>
        /// Tests the alert service configuration
        /// </summary>
        /// <returns>True if the service is properly configured and can send alerts</returns>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                if (!IsConfigurationValid())
                {
                    _logger.LogWarning("Cannot test connection - configuration is invalid");
                    return false;
                }

                var testMessage = $"DeskDefender test connection - {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                await SendAlertAsync(testMessage);
                
                _logger.LogInformation("Test connection successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test connection failed");
                return false;
            }
        }

        /// <summary>
        /// Sends an alert message via SMS
        /// Implements rate limiting and message formatting
        /// Uses the Template Method pattern for consistent message structure
        /// </summary>
        /// <param name="message">Alert message content</param>
        /// <param name="imagePath">Optional path to associated image</param>
        public void SendAlert(string message, string imagePath = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Message cannot be null or empty", nameof(message));
            }

            try
            {
                // Check if SMS is enabled
                if (!_settings.EnableSMS)
                {
                    _logger.LogDebug("SMS alerts are disabled, skipping alert: {Message}", message);
                    return;
                }

                // Validate configuration
                if (!IsConfigurationValid())
                {
                    _logger.LogWarning("Twilio configuration is invalid, cannot send alert");
                    return;
                }

                // Apply rate limiting
                if (!ShouldSendAlert())
                {
                    _logger.LogWarning("Alert rate limit exceeded, skipping alert: {Message}", message);
                    return;
                }

                // Format message using Template Method pattern
                var formattedMessage = FormatAlertMessage(message, imagePath);

                // Send SMS
                var messageResource = MessageResource.Create(
                    body: formattedMessage,
                    from: new PhoneNumber(_settings.TwilioFromNumber),
                    to: new PhoneNumber(_settings.PhoneNumber)
                );

                // Update rate limiting counters
                UpdateRateLimitingCounters();

                _logger.LogInformation("Alert sent successfully via SMS: {MessageSid}", messageResource.Sid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send SMS alert: {Message}", message);
                throw;
            }
        }

        /// <summary>
        /// Asynchronously sends an alert message via SMS
        /// Provides non-blocking operation for high-performance scenarios
        /// </summary>
        /// <param name="message">Alert message content</param>
        /// <param name="imagePath">Optional path to associated image</param>
        public async Task SendAlertAsync(string message, string imagePath = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Message cannot be null or empty", nameof(message));
            }

            try
            {
                // Check if SMS is enabled
                if (!_settings.EnableSMS)
                {
                    _logger.LogDebug("SMS alerts are disabled, skipping alert: {Message}", message);
                    return;
                }

                // Validate configuration
                if (!IsConfigurationValid())
                {
                    _logger.LogWarning("Twilio configuration is invalid, cannot send alert");
                    return;
                }

                // Apply rate limiting
                if (!ShouldSendAlert())
                {
                    _logger.LogWarning("Alert rate limit exceeded, skipping alert: {Message}", message);
                    return;
                }

                // Format message
                var formattedMessage = FormatAlertMessage(message, imagePath);

                // Send SMS asynchronously
                var messageResource = await MessageResource.CreateAsync(
                    body: formattedMessage,
                    from: new PhoneNumber(_settings.TwilioFromNumber),
                    to: new PhoneNumber(_settings.PhoneNumber)
                );

                // Update rate limiting counters
                UpdateRateLimitingCounters();

                _logger.LogInformation("Alert sent successfully via SMS (async): {MessageSid}", messageResource.Sid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send SMS alert asynchronously: {Message}", message);
                throw;
            }
        }

        /// <summary>
        /// Sends an alert for a specific security event
        /// Implements the Factory pattern for event-specific message formatting
        /// </summary>
        /// <param name="securityEvent">Security event to alert about</param>
        public void SendAlert(EventLog securityEvent)
        {
            if (securityEvent == null)
            {
                throw new ArgumentNullException(nameof(securityEvent));
            }

            try
            {
                // Generate event-specific message
                var message = GenerateEventMessage(securityEvent);
                
                // Send alert with image if available
                SendAlert(message, securityEvent.ImagePath);
                
                _logger.LogDebug("Event alert sent for: {EventType} - {EventId}", 
                    securityEvent.EventType, securityEvent.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send event alert: {EventType} - {EventId}", 
                    securityEvent.EventType, securityEvent.Id);
                throw;
            }
        }

        /// <summary>
        /// Asynchronously sends an alert for a specific security event
        /// </summary>
        /// <param name="securityEvent">Security event to alert about</param>
        public async Task SendAlertAsync(EventLog securityEvent)
        {
            if (securityEvent == null)
            {
                throw new ArgumentNullException(nameof(securityEvent));
            }

            try
            {
                // Generate event-specific message
                var message = GenerateEventMessage(securityEvent);
                
                // Send alert with image if available
                await SendAlertAsync(message, securityEvent.ImagePath);
                
                _logger.LogDebug("Event alert sent asynchronously for: {EventType} - {EventId}", 
                    securityEvent.EventType, securityEvent.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send event alert asynchronously: {EventType} - {EventId}", 
                    securityEvent.EventType, securityEvent.Id);
                throw;
            }
        }

        /// <summary>
        /// Tests the alert service configuration
        /// Useful for validating setup during configuration
        /// </summary>
        /// <returns>True if test message was sent successfully</returns>
        public bool TestAlert()
        {
            try
            {
                if (!IsConfigurationValid())
                {
                    _logger.LogWarning("Cannot test alert - configuration is invalid");
                    return false;
                }

                var testMessage = $"DeskDefender test alert - {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                SendAlert(testMessage);
                
                _logger.LogInformation("Test alert sent successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test alert failed");
                return false;
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Initializes the Twilio client with configuration settings
        /// Implements lazy initialization for performance
        /// </summary>
        private void InitializeTwilio()
        {
            lock (_lockObject)
            {
                if (_isInitialized)
                {
                    return;
                }

                try
                {
                    if (IsConfigurationValid())
                    {
                        TwilioClient.Init(_settings.TwilioAccountSid, _settings.TwilioAuthToken);
                        _isInitialized = true;
                        _logger.LogInformation("Twilio client initialized successfully");
                    }
                    else
                    {
                        _logger.LogWarning("Twilio configuration is incomplete, client not initialized");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Twilio client");
                    throw;
                }
            }
        }

        /// <summary>
        /// Validates that all required Twilio configuration is present
        /// Implements the Validation pattern for configuration checking
        /// </summary>
        /// <returns>True if configuration is valid</returns>
        private bool IsConfigurationValid()
        {
            return !string.IsNullOrWhiteSpace(_settings.TwilioAccountSid) &&
                   !string.IsNullOrWhiteSpace(_settings.TwilioAuthToken) &&
                   !string.IsNullOrWhiteSpace(_settings.TwilioFromNumber) &&
                   !string.IsNullOrWhiteSpace(_settings.PhoneNumber);
        }

        /// <summary>
        /// Determines if an alert should be sent based on rate limiting rules
        /// Implements rate limiting to prevent SMS spam and reduce costs
        /// </summary>
        /// <returns>True if alert should be sent</returns>
        private bool ShouldSendAlert()
        {
            var now = DateTime.UtcNow;
            
            // Check minimum interval between alerts
            if (now - _lastAlertTime < _minimumAlertInterval)
            {
                return false;
            }

            // Reset hourly counter if needed
            if (now.Hour != _lastAlertTime.Hour)
            {
                _alertCount = 0;
            }

            // Check hourly limit
            if (_alertCount >= _maxAlertsPerHour)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Updates rate limiting counters after sending an alert
        /// </summary>
        private void UpdateRateLimitingCounters()
        {
            _lastAlertTime = DateTime.UtcNow;
            _alertCount++;
        }

        /// <summary>
        /// Formats an alert message using the Template Method pattern
        /// Provides consistent message structure across all alerts
        /// </summary>
        /// <param name="message">Core message content</param>
        /// <param name="imagePath">Optional image path</param>
        /// <returns>Formatted alert message</returns>
        private string FormatAlertMessage(string message, string imagePath = null)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var formattedMessage = $"🛡️ DeskDefender Alert\n" +
                                 $"Time: {timestamp}\n" +
                                 $"Alert: {message}";

            // Add image information if available
            if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
            {
                formattedMessage += $"\n📷 Image captured: {Path.GetFileName(imagePath)}";
            }

            // Add system information
            formattedMessage += $"\n💻 System: {Environment.MachineName}";

            return formattedMessage;
        }

        /// <summary>
        /// Generates event-specific alert messages using the Factory pattern
        /// Creates tailored messages based on event type and severity
        /// </summary>
        /// <param name="securityEvent">Security event to generate message for</param>
        /// <returns>Event-specific alert message</returns>
        private string GenerateEventMessage(EventLog securityEvent)
        {
            var severityIcon = GetSeverityIcon(securityEvent.Severity);
            var baseMessage = $"{severityIcon} {securityEvent.EventType} Event Detected";

            // Add event-specific details based on type
            switch (securityEvent)
            {
                case InputEvent inputEvent:
                    return $"{baseMessage}\n" +
                           $"Activity: {inputEvent.KeystrokeCount} keystrokes, {inputEvent.MouseClickCount} clicks\n" +
                           $"Duration: {inputEvent.Duration:mm\\:ss}\n" +
                           $"Idle before: {inputEvent.PreviousIdleTime:hh\\:mm\\:ss}";

                case CameraEvent cameraEvent:
                    return $"{baseMessage}\n" +
                           $"Detection: {cameraEvent.DetectionType}\n" +
                           $"Confidence: {cameraEvent.Confidence:P0}\n" +
                           $"Motion areas: {cameraEvent.MotionAreas}";

                case LoginEvent loginEvent:
                    return $"{baseMessage}\n" +
                           $"User: {loginEvent.Username}\n" +
                           $"Success: {(loginEvent.Success ? "Yes" : "No")}\n" +
                           $"Source: {loginEvent.Source}";

                case UsbEvent usbEvent:
                    return $"{baseMessage}\n" +
                           $"Device: {usbEvent.DeviceName}\n" +
                           $"Action: {(usbEvent.Connected ? "Connected" : "Disconnected")}";

                default:
                    return $"{baseMessage}\n{securityEvent.Description}";
            }
        }

        /// <summary>
        /// Gets an appropriate emoji icon for event severity
        /// Provides visual indication of threat level
        /// </summary>
        /// <param name="severity">Event severity level</param>
        /// <returns>Emoji icon representing severity</returns>
        private string GetSeverityIcon(EventSeverity severity)
        {
            return severity switch
            {
                EventSeverity.Critical => "🚨",
                EventSeverity.High => "⚠️",
                EventSeverity.Medium => "⚡",
                EventSeverity.Low => "ℹ️",
                EventSeverity.Info => "📋",
                _ => "🔔"
            };
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Cleans up resources used by the alert service
        /// Implements the Dispose pattern
        /// </summary>
        public void Dispose()
        {
            // Twilio client doesn't require explicit disposal
            // but we can log the cleanup
            _logger.LogDebug("TwilioAlertService disposed");
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
