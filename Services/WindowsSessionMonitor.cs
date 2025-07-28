using System;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;

namespace DeskDefender.Services
{
    /// <summary>
    /// Windows implementation of session state monitoring
    /// Monitors session lock/unlock events using SystemEvents.SessionSwitch
    /// </summary>
    public class WindowsSessionMonitor : ISessionMonitor, IDisposable
    {
        #region Private Fields

        private readonly ILogger<WindowsSessionMonitor> _logger;
        private readonly IEventLogger _eventLogger;
        private bool _isMonitoring = false;
        private bool _isSessionLocked = false;
        private bool _disposed = false;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the WindowsSessionMonitor
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="eventLogger">Event logger for recording session events</param>
        public WindowsSessionMonitor(ILogger<WindowsSessionMonitor> logger, IEventLogger eventLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
            
            _logger.LogInformation("WindowsSessionMonitor initialized");
        }

        #endregion

        #region ISessionMonitor Implementation

        /// <summary>
        /// Event fired when session state changes
        /// </summary>
        public event EventHandler<SessionStateChangedEventArgs>? SessionStateChanged;

        /// <summary>
        /// Gets whether the current session is locked
        /// </summary>
        public bool IsSessionLocked => _isSessionLocked;

        /// <summary>
        /// Gets whether session monitoring is currently active
        /// </summary>
        public bool IsMonitoring => _isMonitoring;

        /// <summary>
        /// Starts monitoring session state changes
        /// </summary>
        public void StartMonitoring()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WindowsSessionMonitor));

            if (_isMonitoring)
            {
                _logger.LogWarning("Session monitoring is already running");
                return;
            }

            try
            {
                // Subscribe to Windows session switch events
                SystemEvents.SessionSwitch += OnSessionSwitch;
                _isMonitoring = true;
                
                _logger.LogInformation("Session monitoring started successfully");
                
                // Log the start of session monitoring
                LogSessionEvent(SessionState.Unlocked, "Session monitoring started - assuming unlocked state");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start session monitoring");
                throw;
            }
        }

        /// <summary>
        /// Stops monitoring session state changes
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isMonitoring)
                return;

            try
            {
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                _isMonitoring = false;
                
                _logger.LogInformation("Session monitoring stopped");
                
                // Log the stop of session monitoring
                LogSessionEvent(SessionState.Unlocked, "Session monitoring stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping session monitoring");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles Windows session switch events
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Session switch event arguments</param>
        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            try
            {
                var sessionState = MapSessionSwitchReason(e.Reason);
                var timestamp = DateTime.Now;
                var context = $"Windows session switch: {e.Reason}";

                // Update internal state
                _isSessionLocked = (sessionState == SessionState.Locked);

                _logger.LogInformation("Session state changed: {Reason} -> {State} at {Timestamp}", 
                    e.Reason, sessionState, timestamp);

                // Log the session event to database
                LogSessionEvent(sessionState, context);

                // Fire the event to notify subscribers
                var args = new SessionStateChangedEventArgs
                {
                    NewState = sessionState,
                    Timestamp = timestamp,
                    Context = context
                };

                SessionStateChanged?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling session switch event: {Reason}", e.Reason);
            }
        }

        /// <summary>
        /// Maps Windows SessionSwitchReason to our SessionState enum
        /// </summary>
        /// <param name="reason">Windows session switch reason</param>
        /// <returns>Corresponding session state</returns>
        private static SessionState MapSessionSwitchReason(SessionSwitchReason reason)
        {
            return reason switch
            {
                SessionSwitchReason.SessionLock => SessionState.Locked,
                SessionSwitchReason.SessionUnlock => SessionState.Unlocked,
                SessionSwitchReason.RemoteConnect => SessionState.RemoteConnect,
                SessionSwitchReason.RemoteDisconnect => SessionState.RemoteDisconnect,
                SessionSwitchReason.SessionLogon => SessionState.Logon,
                SessionSwitchReason.SessionLogoff => SessionState.Logoff,
                _ => SessionState.Unlocked // Default to unlocked for unknown states
            };
        }

        /// <summary>
        /// Logs session events to the database
        /// </summary>
        /// <param name="state">Session state</param>
        /// <param name="context">Additional context</param>
        private void LogSessionEvent(SessionState state, string context)
        {
            try
            {
                var severity = state switch
                {
                    SessionState.Locked => EventSeverity.Info,
                    SessionState.Unlocked => EventSeverity.Info,
                    SessionState.RemoteConnect => EventSeverity.Medium,
                    SessionState.RemoteDisconnect => EventSeverity.Medium,
                    SessionState.Logon => EventSeverity.Low,
                    SessionState.Logoff => EventSeverity.Low,
                    _ => EventSeverity.Info
                };

                var eventLog = new EventLog
                {
                    EventType = "Session",
                    Description = $"Session state changed to: {state}",
                    Details = context,
                    Severity = severity,
                    Source = "WindowsSessionMonitor",
                    Timestamp = DateTime.Now,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        SessionState = state.ToString(),
                        IsLocked = _isSessionLocked,
                        Context = context
                    })
                };

                _eventLogger.LogEventAsync(eventLog);
                
                _logger.LogDebug("Session event logged: {State} - {Description}", state, eventLog.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log session event: {State}", state);
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes the session monitor and releases resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        /// <param name="disposing">Whether disposing from Dispose() call</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                StopMonitoring();
                _disposed = true;
                _logger.LogDebug("WindowsSessionMonitor disposed");
            }
        }

        #endregion
    }
}
