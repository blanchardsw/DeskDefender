using System;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for system tray functionality
    /// </summary>
    public interface ITrayService
    {
        /// <summary>
        /// Event fired when user requests to show the main window from tray
        /// </summary>
        event EventHandler? ShowMainWindow;

        /// <summary>
        /// Event fired when user requests to exit the application from tray
        /// </summary>
        event EventHandler? ExitApplication;

        /// <summary>
        /// Event fired when user toggles monitoring from tray
        /// </summary>
        event EventHandler? ToggleMonitoring;

        /// <summary>
        /// Gets whether the tray icon is currently visible
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// Initializes the tray service
        /// </summary>
        void Initialize();

        /// <summary>
        /// Minimizes the application to system tray
        /// </summary>
        void MinimizeToTray();

        /// <summary>
        /// Restores the application from system tray
        /// </summary>
        void RestoreFromTray();

        /// <summary>
        /// Shows a tray notification
        /// </summary>
        /// <param name="title">Notification title</param>
        /// <param name="message">Notification message</param>
        /// <param name="timeout">Timeout in milliseconds (default 3000)</param>
        void ShowTrayNotification(string title, string message, int timeout = 3000);

        /// <summary>
        /// Updates the monitoring status in the tray context menu
        /// </summary>
        /// <param name="isMonitoring">Whether monitoring is currently active</param>
        void UpdateMonitoringStatus(bool isMonitoring);

        /// <summary>
        /// Disposes the tray service and hides the tray icon
        /// </summary>
        void Dispose();
    }
}
