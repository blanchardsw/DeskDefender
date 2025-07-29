using System;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Security.Principal;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Windows implementation of login attempt monitoring
    /// Monitors Windows Security Event Log for login attempts (successful and failed)
    /// </summary>
    public class WindowsLoginMonitor : ILoginMonitor, IDisposable
    {
        #region Private Fields

        private readonly ILogger<WindowsLoginMonitor> _logger;
        private readonly IEventLogger _eventLogger;
        private System.Diagnostics.EventLog? _securityEventLog;
        private bool _isMonitoring = false;
        private bool _disposed = false;
        
        // Default Windows Event IDs for login monitoring
        private int[] _monitoredEventIds = { 4624, 4625, 4634, 4647 }; // Logon success, failure, logoff, user-initiated logoff

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the WindowsLoginMonitor
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="eventLogger">Event logging service</param>
        public WindowsLoginMonitor(
            ILogger<WindowsLoginMonitor> logger,
            IEventLogger eventLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
            
            // Set comprehensive Event IDs for all login types including PIN
            _monitoredEventIds = new int[]
            {
                4624, // Successful logon
                4625, // Failed logon
                4634, // Logoff
                4647, // User initiated logoff
                4648, // Logon using explicit credentials
                4776, // Domain controller attempted to validate credentials (PIN/Smart Card)
                4777, // Domain controller failed to validate credentials (PIN/Smart Card)
                5379, // Credential Manager credentials were read (PIN unlock)
                5380, // Vault credentials were read (PIN unlock)
                5632, // Wireless LAN 802.1x authentication (can include PIN)
                5633  // Wired LAN 802.1x authentication (can include PIN)
            };
            
            _logger.LogInformation("WindowsLoginMonitor initialized with comprehensive Event IDs: {EventIds}", 
                string.Join(", ", _monitoredEventIds));
        }

        #endregion

        #region ILoginMonitor Implementation

        /// <summary>
        /// Event fired when a login attempt is detected
        /// </summary>
        public event EventHandler<LoginEvent>? LoginAttemptDetected;

        /// <summary>
        /// Sets which event IDs to monitor (e.g., 4625 for failed logins)
        /// </summary>
        /// <param name="eventIds">Array of Windows Event Log IDs to monitor</param>
        public void SetMonitoredEventIds(int[] eventIds)
        {
            _monitoredEventIds = eventIds ?? throw new ArgumentNullException(nameof(eventIds));
            _logger.LogInformation("Login monitor configured to watch Event IDs: {EventIds}", string.Join(", ", eventIds));
        }

        #endregion

        #region IMonitorService Implementation

        /// <summary>
        /// Gets whether the login monitoring service is currently running
        /// </summary>
        public bool IsRunning => _isMonitoring;

        /// <summary>
        /// Event fired when the service status changes
        /// </summary>
        public event EventHandler<bool> StatusChanged;

        /// <summary>
        /// Starts monitoring Windows login attempts
        /// </summary>
        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WindowsLoginMonitor));

            if (_isMonitoring)
            {
                _logger.LogWarning("Login monitoring is already running");
                return;
            }

            try
            {
                _logger.LogInformation("Starting Windows login monitoring with Event IDs: {EventIds}...", 
                    string.Join(", ", _monitoredEventIds));

                // Check if running with administrator privileges
                var isAdmin = IsRunningAsAdministrator();
                _logger.LogInformation("Administrator privileges: {IsAdmin}", isAdmin);

                if (!isAdmin)
                {
                    _logger.LogWarning("Login monitoring requires administrator privileges for full functionality. " +
                        "Some login events (especially failed attempts) may not be captured.");
                    
                    // Log a warning event but continue with limited functionality
                    LogLoginMonitoringEvent("Login monitoring started with limited privileges - some events may not be captured", EventSeverity.Warning);
                }

                // Initialize Security Event Log monitoring
                _securityEventLog = new System.Diagnostics.EventLog("Security");
                _securityEventLog.EntryWritten += OnSecurityEventWritten;
                _securityEventLog.EnableRaisingEvents = true;

                _isMonitoring = true;
                StatusChanged?.Invoke(this, true);
                
                _logger.LogInformation("Windows login monitoring started successfully (Admin: {IsAdmin})", isAdmin);
                
                // Log the start event
                var startMessage = isAdmin ? "Login monitoring started with full privileges" : "Login monitoring started with limited privileges";
                LogLoginMonitoringEvent(startMessage, isAdmin ? EventSeverity.Info : EventSeverity.Warning);
            }
            catch (SecurityException ex)
            {
                _logger.LogError(ex, "Access denied to Security Event Log - login monitoring requires administrator privileges");
                
                // Log the error but don't throw - allow the app to continue with other monitoring
                LogLoginMonitoringEvent("Login monitoring failed: Administrator privileges required", EventSeverity.Critical);
                
                _logger.LogWarning("Login monitoring will be disabled. Run the application as Administrator to enable login attempt detection.");
                return; // Don't throw, just disable login monitoring
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start login monitoring");
                LogLoginMonitoringEvent($"Login monitoring failed: {ex.Message}", EventSeverity.Critical);
                throw;
            }
        }

        /// <summary>
        /// Stops monitoring Windows login attempts
        /// </summary>
        public void Stop()
        {
            if (!_isMonitoring)
                return;

            try
            {
                _logger.LogInformation("Stopping Windows login monitoring...");

                if (_securityEventLog != null)
                {
                    _securityEventLog.EnableRaisingEvents = false;
                    _securityEventLog.EntryWritten -= OnSecurityEventWritten;
                    _securityEventLog.Dispose();
                    _securityEventLog = null;
                }

                _isMonitoring = false;
                StatusChanged?.Invoke(this, false);
                
                _logger.LogInformation("Windows login monitoring stopped");
                
                // Log the stop event
                LogLoginMonitoringEvent("Login monitoring stopped", EventSeverity.Info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping login monitoring");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Checks if the current process is running with administrator privileges
        /// </summary>
        /// <returns>True if running as administrator, false otherwise</returns>
        private bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine administrator privileges, assuming non-admin");
                return false;
            }
        }

        /// <summary>
        /// Handles Windows Security Event Log entries
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event log entry written event arguments</param>
        private void OnSecurityEventWritten(object sender, System.Diagnostics.EntryWrittenEventArgs e)
        {
            try
            {
                var entry = e.Entry;
                
                // Check if this is a login-related event we're monitoring
                if (!_monitoredEventIds.Contains((int)entry.InstanceId))
                    return;

                var loginEvent = ParseLoginEvent(entry);
                if (loginEvent != null)
                {
                    _logger.LogInformation("Login event detected: {Success} for user {Username} at {Timestamp}", 
                        loginEvent.Success ? "Success" : "Failure", loginEvent.Username, loginEvent.Timestamp);

                    // Fire the event to notify subscribers
                    LoginAttemptDetected?.Invoke(this, loginEvent);
                    
                    // Log to database
                    LogLoginEvent(loginEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing security event log entry");
            }
        }

        /// <summary>
        /// Parses a Windows Security Event Log entry into a LoginEvent
        /// </summary>
        /// <param name="entry">Windows Event Log entry</param>
        /// <returns>Parsed LoginEvent or null if not a login event</returns>
        private LoginEvent? ParseLoginEvent(System.Diagnostics.EventLogEntry entry)
        {
            try
            {
                var loginType = (int)entry.InstanceId switch
                {
                    4624 => LoginEventType.Success,
                    4625 => LoginEventType.Failure,
                    4634 => LoginEventType.Logoff,
                    4647 => LoginEventType.UserLogoff,
                    _ => LoginEventType.Unknown
                };

                if (loginType == LoginEventType.Unknown)
                    return null;

                // Extract username from event message (this is a simplified approach)
                var username = ExtractUsernameFromMessage(entry.Message);
                var workstation = ExtractWorkstationFromMessage(entry.Message);

                return new LoginEvent
                {
                    Username = username,
                    Success = loginType == LoginEventType.Success,
                    EventId = (int)entry.InstanceId,
                    WorkstationName = workstation,
                    LogonType = ExtractLogonTypeFromMessage(entry.Message),
                    FailureReason = loginType == LoginEventType.Failure ? ExtractFailureReasonFromMessage(entry.Message) : null,
                    Timestamp = entry.TimeGenerated,
                    Description = $"{loginType} login attempt for user: {username}",
                    Details = entry.Message,
                    Severity = loginType == LoginEventType.Failure ? EventSeverity.High : EventSeverity.Info
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse login event from Event Log entry");
                return null;
            }
        }

        /// <summary>
        /// Extracts username from Windows Event Log message
        /// </summary>
        /// <param name="message">Event log message</param>
        /// <returns>Extracted username or "Unknown"</returns>
        private string ExtractUsernameFromMessage(string message)
        {
            try
            {
                // Look for "Account Name:" pattern in the message
                var lines = message.Split('\n', '\r');
                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("Account Name:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            var username = parts[1].Trim();
                            if (!string.IsNullOrEmpty(username) && username != "-")
                                return username;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to extract username from event message");
            }

            return "Unknown";
        }

        /// <summary>
        /// Extracts workstation name from Windows Event Log message
        /// </summary>
        /// <param name="message">Event log message</param>
        /// <returns>Extracted workstation name or "Unknown"</returns>
        private string ExtractWorkstationFromMessage(string message)
        {
            try
            {
                // Look for "Workstation Name:" pattern in the message
                var lines = message.Split('\n', '\r');
                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("Workstation Name:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            var workstation = parts[1].Trim();
                            if (!string.IsNullOrEmpty(workstation) && workstation != "-")
                                return workstation;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to extract workstation from event message");
            }

            return Environment.MachineName;
        }

        /// <summary>
        /// Extracts logon type from Windows Event Log message
        /// </summary>
        /// <param name="message">Event log message</param>
        /// <returns>Extracted logon type or "Unknown"</returns>
        private string ExtractLogonTypeFromMessage(string message)
        {
            try
            {
                // Look for "Logon Type:" pattern in the message
                var lines = message.Split('\n', '\r');
                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("Logon Type:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            var logonType = parts[1].Trim();
                            if (!string.IsNullOrEmpty(logonType))
                                return logonType;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to extract logon type from event message");
            }

            return "Unknown";
        }

        /// <summary>
        /// Extracts failure reason from Windows Event Log message for failed login attempts
        /// </summary>
        /// <param name="message">Event log message</param>
        /// <returns>Extracted failure reason or "Unknown"</returns>
        private string ExtractFailureReasonFromMessage(string message)
        {
            try
            {
                // Look for "Failure Reason:" or "Status:" pattern in the message
                var lines = message.Split('\n', '\r');
                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("Failure Reason:", StringComparison.OrdinalIgnoreCase) ||
                        line.Trim().StartsWith("Status:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            var reason = parts[1].Trim();
                            if (!string.IsNullOrEmpty(reason))
                                return reason;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to extract failure reason from event message");
            }

            return "Unknown";
        }

        /// <summary>
        /// Logs login events to the database
        /// </summary>
        /// <param name="loginEvent">Login event to log</param>
        private async void LogLoginEvent(LoginEvent loginEvent)
        {
            try
            {
                // The LoginEvent already extends EventLog, so we can log it directly
                // Just ensure the metadata is properly set
                loginEvent.Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Username = loginEvent.Username,
                    WorkstationName = loginEvent.WorkstationName,
                    EventId = loginEvent.EventId,
                    Success = loginEvent.Success,
                    LogonType = loginEvent.LogonType,
                    FailureReason = loginEvent.FailureReason
                });

                await _eventLogger.LogEventAsync(loginEvent);
                
                _logger.LogDebug("Login event logged: {Success} for {Username}", loginEvent.Success ? "Success" : "Failure", loginEvent.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log login event: {Success} for {Username}", loginEvent.Success ? "Success" : "Failure", loginEvent.Username);
            }
        }

        /// <summary>
        /// Logs login monitoring service events to the database
        /// </summary>
        /// <param name="description">Event description</param>
        /// <param name="severity">Event severity</param>
        private void LogLoginMonitoringEvent(string description, EventSeverity severity)
        {
            try
            {
                var eventLog = new Models.Events.EventLog
                {
                    EventType = "System",
                    Description = description,
                    Details = $"Windows Login Monitor service event at {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    Severity = severity,
                    Source = "WindowsLoginMonitor",
                    Timestamp = DateTime.Now,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        ServiceEvent = true,
                        MonitoredEventIds = _monitoredEventIds
                    })
                };

                _eventLogger.LogEventAsync(eventLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log login monitoring service event: {Description}", description);
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the login monitor and releases resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                Stop();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login monitor disposal");
            }

            _disposed = true;
        }

        #endregion
    }
}
