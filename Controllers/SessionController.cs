using System;
using System.Windows.Threading;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Controllers
{
    /// <summary>
    /// Handles session state changes and related UI updates
    /// Extracted from MainWindow to improve separation of concerns
    /// </summary>
    public class SessionController
    {
        private readonly ILogger<SessionController> _logger;
        private readonly ISessionMonitor _sessionMonitor;
        private readonly ITrayService _trayService;
        private readonly Dispatcher _dispatcher;

        public SessionController(
            ILogger<SessionController> logger,
            ISessionMonitor sessionMonitor,
            ITrayService trayService,
            Dispatcher dispatcher)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionMonitor = sessionMonitor ?? throw new ArgumentNullException(nameof(sessionMonitor));
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>
        /// Initializes session monitoring and subscribes to events
        /// </summary>
        public void Initialize()
        {
            try
            {
                _sessionMonitor.SessionStateChanged += OnSessionStateChanged;
                _logger.LogInformation("Session controller initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize session controller");
            }
        }

        /// <summary>
        /// Handles session state changes from the session monitor
        /// </summary>
        private void OnSessionStateChanged(object? sender, SessionStateChangedEventArgs e)
        {
            try
            {
                _dispatcher.Invoke(() =>
                {
                    _logger.LogInformation("Session state changed: {State} at {Timestamp}", e.NewState, e.Timestamp);
                    
                    // Update UI to reflect session state
                    var sessionStatus = e.NewState switch
                    {
                        SessionState.Locked => "🔒 Session Locked - Background monitoring active",
                        SessionState.Unlocked => "🔓 Session Unlocked - Full monitoring active",
                        SessionState.RemoteConnect => "🌐 Remote session connected",
                        SessionState.RemoteDisconnect => "🌐 Remote session disconnected",
                        SessionState.Logon => "👤 User logged on",
                        SessionState.Logoff => "👤 User logged off",
                        _ => $"Session: {e.NewState}"
                    };
                    
                    // Show tray notification for important session changes
                    if (e.NewState == SessionState.Locked || e.NewState == SessionState.Unlocked)
                    {
                        _trayService.ShowTrayNotification(
                            "DeskDefender Session Change",
                            sessionStatus,
                            3000);
                    }
                    
                    _logger.LogDebug("UI updated for session state: {Status}", sessionStatus);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling session state change in UI");
            }
        }

        /// <summary>
        /// Gets the current session state for display purposes
        /// </summary>
        public string GetCurrentSessionStatus()
        {
            try
            {
                // This would need to be implemented based on your session monitor's current state
                // For now, return a placeholder
                return "Session monitoring active";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current session status");
                return "Session status unknown";
            }
        }

        /// <summary>
        /// Cleanup method to unsubscribe from events
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (_sessionMonitor != null)
                {
                    _sessionMonitor.SessionStateChanged -= OnSessionStateChanged;
                }
                _logger.LogDebug("Session controller cleaned up successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session controller cleanup");
            }
        }
    }
}
