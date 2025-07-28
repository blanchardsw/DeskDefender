using System;
using Microsoft.Extensions.Logging;
using DeskDefender.Interfaces;

namespace DeskDefender.Services
{
    /// <summary>
    /// No-operation tray service that implements the interface but doesn't actually create a tray icon
    /// This allows the application to build and run without tray functionality
    /// </summary>
    public class NoOpTrayService : ITrayService, IDisposable
    {
        private readonly ILogger<NoOpTrayService> _logger;
        private bool _disposed = false;

        public event EventHandler? ShowMainWindow;
        public event EventHandler? ExitApplication;
        public event EventHandler? ToggleMonitoring;

        public bool IsVisible { get; private set; } = false;

        public NoOpTrayService(ILogger<NoOpTrayService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Initialize()
        {
            // No-op: Just log that this was called
            _logger.LogDebug("Initialize called (no-op implementation)");
        }

        public void MinimizeToTray()
        {
            // No-op: Just log that this was called
            _logger.LogDebug("MinimizeToTray called (no-op implementation)");
            IsVisible = true;
        }

        public void RestoreFromTray()
        {
            // No-op: Just log that this was called
            _logger.LogDebug("RestoreFromTray called (no-op implementation)");
        }

        public void ShowTrayNotification(string title, string message, int timeout = 3000)
        {
            // No-op: Just log the notification
            _logger.LogDebug("Tray notification (no-op): {Title} - {Message}", title, message);
        }

        public void UpdateMonitoringStatus(bool isMonitoring)
        {
            // No-op: Just log the status update
            _logger.LogDebug("Monitoring status updated (no-op): {IsMonitoring}", isMonitoring);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _logger.LogDebug("No-op tray service disposed");
                }
                _disposed = true;
            }
        }

        ~NoOpTrayService()
        {
            Dispose(false);
        }
    }
}
