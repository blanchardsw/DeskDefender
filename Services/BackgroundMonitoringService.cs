using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;

namespace DeskDefender.Services
{
    /// <summary>
    /// Background monitoring service that coordinates all monitoring services
    /// during session state changes and ensures continuous operation
    /// </summary>
    public class BackgroundMonitoringService : IBackgroundMonitoringService, IDisposable
    {
        #region Private Fields

        private readonly ILogger<BackgroundMonitoringService> _logger;
        private readonly ISessionMonitor _sessionMonitor;
        private readonly IInputMonitor _inputMonitor;
        private readonly ICameraService _cameraService;
        private readonly IEventLogger _eventLogger;
        private readonly ITrayService _trayService;
        
        private bool _isBackgroundMonitoringActive = false;
        private bool _disposed = false;
        private BackgroundMonitoringStatus _currentStatus = new();

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the BackgroundMonitoringService
        /// </summary>
        public BackgroundMonitoringService(
            ILogger<BackgroundMonitoringService> logger,
            ISessionMonitor sessionMonitor,
            IInputMonitor inputMonitor,
            ICameraService cameraService,
            IEventLogger eventLogger,
            ITrayService trayService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionMonitor = sessionMonitor ?? throw new ArgumentNullException(nameof(sessionMonitor));
            _inputMonitor = inputMonitor ?? throw new ArgumentNullException(nameof(inputMonitor));
            _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));

            // Subscribe to session state changes
            _sessionMonitor.SessionStateChanged += OnSessionStateChanged;
            
            _logger.LogInformation("BackgroundMonitoringService initialized");
        }

        #endregion

        #region IBackgroundMonitoringService Implementation

        /// <summary>
        /// Event fired when background monitoring status changes
        /// </summary>
        public event EventHandler<BackgroundMonitoringStatusEventArgs>? StatusChanged;

        /// <summary>
        /// Gets whether background monitoring is currently active
        /// </summary>
        public bool IsBackgroundMonitoringActive => _isBackgroundMonitoringActive;

        /// <summary>
        /// Ensures continuous monitoring across all services
        /// </summary>
        public void EnsureContinuousMonitoring()
        {
            try
            {
                _logger.LogInformation("Ensuring continuous monitoring across all services");

                // Update current status
                UpdateMonitoringStatus();

                // Ensure critical services are running
                if (!_currentStatus.InputMonitoringActive)
                {
                    _logger.LogWarning("Input monitoring is not active - attempting to restart");
                    try
                    {
                        _inputMonitor.Start();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to restart input monitoring");
                    }
                }

                if (!_currentStatus.SessionMonitoringActive)
                {
                    _logger.LogWarning("Session monitoring is not active - attempting to restart");
                    try
                    {
                        _sessionMonitor.StartMonitoring();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to restart session monitoring");
                    }
                }

                // Camera service may fail during locked sessions - handle gracefully
                if (!_currentStatus.CameraMonitoringActive && !_currentStatus.IsSessionLocked)
                {
                    _logger.LogInformation("Camera monitoring is not active and session is unlocked - attempting to restart");
                    try
                    {
                        _cameraService.Start();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Camera monitoring could not be restarted - this is expected during locked sessions");
                    }
                }

                // Update status after ensuring services
                UpdateMonitoringStatus();
                
                _logger.LogInformation("Continuous monitoring check completed. Active services: {Count}/4", 
                    _currentStatus.ActiveServicesCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring continuous monitoring");
            }
        }

        /// <summary>
        /// Handles session state changes and adjusts monitoring accordingly
        /// </summary>
        /// <param name="newState">The new session state</param>
        public void HandleSessionStateChange(SessionState newState)
        {
            try
            {
                _logger.LogInformation("Handling session state change: {NewState}", newState);

                _currentStatus.CurrentSessionState = newState;
                _currentStatus.IsSessionLocked = (newState == SessionState.Locked);

                switch (newState)
                {
                    case SessionState.Locked:
                        HandleSessionLocked();
                        break;

                    case SessionState.Unlocked:
                        HandleSessionUnlocked();
                        break;

                    case SessionState.RemoteConnect:
                    case SessionState.RemoteDisconnect:
                        HandleRemoteSessionChange(newState);
                        break;

                    case SessionState.Logon:
                    case SessionState.Logoff:
                        HandleLogonChange(newState);
                        break;
                }

                // Update monitoring status and notify subscribers
                UpdateMonitoringStatus();
                NotifyStatusChanged($"Session state changed to {newState}");

                // Show tray notification for important state changes
                if (newState == SessionState.Locked || newState == SessionState.Unlocked)
                {
                    _trayService.ShowTrayNotification(
                        "DeskDefender", 
                        $"Session {(newState == SessionState.Locked ? "locked" : "unlocked")} - monitoring continues",
                        2000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling session state change: {NewState}", newState);
            }
        }

        /// <summary>
        /// Starts background monitoring coordination
        /// </summary>
        public void StartBackgroundMonitoring()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BackgroundMonitoringService));

            if (_isBackgroundMonitoringActive)
            {
                _logger.LogWarning("Background monitoring is already active");
                return;
            }

            try
            {
                _logger.LogInformation("Starting background monitoring coordination");

                // Start session monitoring first
                _sessionMonitor.StartMonitoring();

                // Ensure all services are running
                EnsureContinuousMonitoring();

                _isBackgroundMonitoringActive = true;
                
                // Log the start event
                LogBackgroundMonitoringEvent("Background monitoring started", EventSeverity.Info);
                
                _logger.LogInformation("Background monitoring coordination started successfully");
                
                NotifyStatusChanged("Background monitoring started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start background monitoring");
                throw;
            }
        }

        /// <summary>
        /// Stops background monitoring coordination
        /// </summary>
        public void StopBackgroundMonitoring()
        {
            if (!_isBackgroundMonitoringActive)
                return;

            try
            {
                _logger.LogInformation("Stopping background monitoring coordination");

                _isBackgroundMonitoringActive = false;
                
                // Log the stop event
                LogBackgroundMonitoringEvent("Background monitoring stopped", EventSeverity.Info);
                
                UpdateMonitoringStatus();
                NotifyStatusChanged("Background monitoring stopped");
                
                _logger.LogInformation("Background monitoring coordination stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping background monitoring");
            }
        }

        /// <summary>
        /// Gets the current status of all monitoring services
        /// </summary>
        /// <returns>Status information for all services</returns>
        public BackgroundMonitoringStatus GetMonitoringStatus()
        {
            UpdateMonitoringStatus();
            return _currentStatus;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles session state change events from the session monitor
        /// </summary>
        private void OnSessionStateChanged(object? sender, SessionStateChangedEventArgs e)
        {
            HandleSessionStateChange(e.NewState);
        }

        /// <summary>
        /// Handles session lock event
        /// </summary>
        private void HandleSessionLocked()
        {
            _logger.LogInformation("Session locked - adjusting monitoring services");

            // Input monitoring should continue during lock
            // Camera monitoring will likely fail - handle gracefully
            try
            {
                // Attempt to stop camera monitoring gracefully
                _cameraService.Stop();
                _logger.LogInformation("Camera monitoring stopped due to session lock");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping camera monitoring during session lock");
            }

            // Ensure input monitoring continues
            EnsureContinuousMonitoring();
        }

        /// <summary>
        /// Handles session unlock event
        /// </summary>
        private void HandleSessionUnlocked()
        {
            _logger.LogInformation("Session unlocked - restoring full monitoring");

            // Restore camera monitoring
            try
            {
                _cameraService.Start();
                _logger.LogInformation("Camera monitoring restored after session unlock");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore camera monitoring after unlock");
            }

            // Ensure all services are running
            EnsureContinuousMonitoring();
        }

        /// <summary>
        /// Handles remote session changes
        /// </summary>
        private void HandleRemoteSessionChange(SessionState state)
        {
            _logger.LogInformation("Remote session change: {State}", state);
            
            // Remote sessions may have different monitoring capabilities
            // Log as medium severity for security awareness
            LogBackgroundMonitoringEvent($"Remote session {state}", EventSeverity.Medium);
        }

        /// <summary>
        /// Handles logon/logoff changes
        /// </summary>
        private void HandleLogonChange(SessionState state)
        {
            _logger.LogInformation("Logon state change: {State}", state);
            
            if (state == SessionState.Logoff)
            {
                // User is logging off - monitoring may need to be suspended
                _logger.LogWarning("User logoff detected - monitoring services may be affected");
            }
            
            LogBackgroundMonitoringEvent($"User {state}", EventSeverity.Low);
        }

        /// <summary>
        /// Updates the current monitoring status
        /// </summary>
        private void UpdateMonitoringStatus()
        {
            try
            {
                _currentStatus.InputMonitoringActive = _inputMonitor.IsRunning;
                _currentStatus.CameraMonitoringActive = _cameraService.IsRunning;
                _currentStatus.SessionMonitoringActive = _sessionMonitor.IsSessionLocked || true; // Session monitor should always be active
                _currentStatus.EventLoggingActive = true; // Event logging is always available
                _currentStatus.IsSessionLocked = _sessionMonitor.IsSessionLocked;
                _currentStatus.LastUpdated = DateTime.Now;

                _logger.LogDebug("Monitoring status updated: Input={Input}, Camera={Camera}, Session={Session}, EventLog={EventLog}",
                    _currentStatus.InputMonitoringActive,
                    _currentStatus.CameraMonitoringActive,
                    _currentStatus.SessionMonitoringActive,
                    _currentStatus.EventLoggingActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating monitoring status");
            }
        }

        /// <summary>
        /// Notifies subscribers of status changes
        /// </summary>
        private void NotifyStatusChanged(string context)
        {
            try
            {
                var args = new BackgroundMonitoringStatusEventArgs
                {
                    Status = _currentStatus,
                    Context = context,
                    Timestamp = DateTime.Now
                };

                StatusChanged?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying status change subscribers");
            }
        }

        /// <summary>
        /// Logs background monitoring events
        /// </summary>
        private void LogBackgroundMonitoringEvent(string description, EventSeverity severity)
        {
            try
            {
                var eventLog = new EventLog
                {
                    EventType = "BackgroundMonitoring",
                    Description = description,
                    Severity = severity,
                    Source = "BackgroundMonitoringService",
                    Timestamp = DateTime.Now,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        IsBackgroundActive = _isBackgroundMonitoringActive,
                        ActiveServices = _currentStatus.ActiveServicesCount,
                        SessionLocked = _currentStatus.IsSessionLocked,
                        SessionState = _currentStatus.CurrentSessionState.ToString()
                    })
                };

                _eventLogger.LogEventAsync(eventLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log background monitoring event");
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes the background monitoring service
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    StopBackgroundMonitoring();
                    
                    if (_sessionMonitor != null)
                    {
                        _sessionMonitor.SessionStateChanged -= OnSessionStateChanged;
                    }

                    _disposed = true;
                    _logger.LogDebug("BackgroundMonitoringService disposed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing BackgroundMonitoringService");
                }
            }
        }

        #endregion
    }
}
