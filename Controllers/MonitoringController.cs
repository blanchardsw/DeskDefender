using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeskDefender.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Controllers
{
    /// <summary>
    /// Handles monitoring control logic and UI updates
    /// Extracted from MainWindow to improve separation of concerns and modularity
    /// </summary>
    public class MonitoringController
    {
        private readonly ILogger<MonitoringController> _logger;
        private readonly IMonitorService _monitoringService;
        private readonly IBackgroundMonitoringService _backgroundMonitoringService;
        private readonly ITrayService _trayService;
        private readonly Dispatcher _dispatcher;
        
        private DateTime? _monitoringStartTime;

        public MonitoringController(
            ILogger<MonitoringController> logger,
            IMonitorService monitoringService,
            IBackgroundMonitoringService backgroundMonitoringService,
            ITrayService trayService,
            Dispatcher dispatcher)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
            _backgroundMonitoringService = backgroundMonitoringService ?? throw new ArgumentNullException(nameof(backgroundMonitoringService));
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>
        /// Toggles monitoring state between start and stop
        /// </summary>
        public async Task ToggleMonitoringAsync(
            System.Windows.Controls.Button toggleButton,
            System.Windows.Shapes.Ellipse statusIndicator,
            System.Windows.Controls.TextBlock statusText)
        {
            try
            {
                _logger.LogInformation("=== TOGGLE MONITORING CLICKED ===");
                _logger.LogInformation("Current monitoring state: {IsRunning}", _monitoringService.IsRunning);
                
                if (_monitoringService.IsRunning)
                {
                    await StopMonitoringAsync(toggleButton, statusIndicator, statusText);
                }
                else
                {
                    await StartMonitoringAsync(toggleButton, statusIndicator, statusText);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling monitoring");
                // Log error silently for stealth operation
                _logger.LogError(ex, "Toggle monitoring error - continuing silently");
            }
        }

        /// <summary>
        /// Starts monitoring services and updates UI
        /// </summary>
        private async Task StartMonitoringAsync(
            System.Windows.Controls.Button toggleButton,
            System.Windows.Shapes.Ellipse statusIndicator,
            System.Windows.Controls.TextBlock statusText)
        {
            _logger.LogInformation("Starting monitoring services...");
            
            // Update UI to show starting state
            await _dispatcher.BeginInvoke(() =>
            {
                toggleButton.Content = "Starting...";
                toggleButton.IsEnabled = false;
            });

            // Start monitoring in background task to avoid UI blocking
            await Task.Run(async () =>
            {
                try
                {
                    // Start main monitoring service
                    _monitoringService.Start();
                    _monitoringStartTime = DateTime.Now;
                    
                    // Start background monitoring coordination
                    _backgroundMonitoringService.StartBackgroundMonitoring();
                    
                    // Update UI and tray to reflect running state
                    await _dispatcher.BeginInvoke(() =>
                    {
                        UpdateUIForMonitoringState(true, toggleButton, statusIndicator, statusText);
                        _trayService.UpdateMonitoringStatus(true);
                        _logger.LogInformation("Monitoring started successfully at {StartTime}", _monitoringStartTime);
                    });
                }
                catch (Exception startEx)
                {
                    _logger.LogError(startEx, "Failed to start monitoring services");
                    
                    await _dispatcher.BeginInvoke(() =>
                    {
                        toggleButton.Content = "Start Monitoring";
                        toggleButton.IsEnabled = true;
                        // Log monitoring failure silently for stealth operation
                        _logger.LogError(startEx, "Monitoring failed to start - continuing silently");
                    });
                }
            });
        }

        /// <summary>
        /// Stops monitoring services and updates UI
        /// </summary>
        private async Task StopMonitoringAsync(
            System.Windows.Controls.Button toggleButton,
            System.Windows.Shapes.Ellipse statusIndicator,
            System.Windows.Controls.TextBlock statusText)
        {
            _logger.LogInformation("Stopping monitoring services...");
            
            // Stop monitoring
            await Task.Run(() => _monitoringService.Stop());
            
            // Stop background monitoring coordination
            _backgroundMonitoringService.StopBackgroundMonitoring();
            
            // Update UI and tray to reflect stopped state
            await _dispatcher.BeginInvoke(() =>
            {
                UpdateUIForMonitoringState(false, toggleButton, statusIndicator, statusText);
                _trayService.UpdateMonitoringStatus(false);
                _monitoringStartTime = null;
                _logger.LogInformation("Monitoring stopped successfully");
            });
        }

        /// <summary>
        /// Updates the UI elements to reflect the current monitoring state
        /// </summary>
        public void UpdateUIForMonitoringState(
            bool isMonitoring,
            System.Windows.Controls.Button toggleButton,
            System.Windows.Shapes.Ellipse statusIndicator,
            System.Windows.Controls.TextBlock statusText)
        {
            try
            {
                if (isMonitoring)
                {
                    // Monitoring is active - show stop state
                    statusIndicator.Fill = new SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
                    statusText.Text = "Monitoring Active";
                    toggleButton.Content = "Stop Monitoring";
                    toggleButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)); // Red for stop
                    toggleButton.IsEnabled = true;
                }
                else
                {
                    // Monitoring is stopped - show start state
                    statusIndicator.Fill = new SolidColorBrush(System.Windows.Media.Colors.Red);
                    statusText.Text = "Monitoring Stopped";
                    toggleButton.Content = "Start Monitoring";
                    toggleButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96)); // Green for start
                    toggleButton.IsEnabled = true;
                }
                
                _logger.LogDebug("UI updated for monitoring state: {IsMonitoring}", isMonitoring);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating UI for monitoring state");
            }
        }

        /// <summary>
        /// Gets the current monitoring uptime for display
        /// </summary>
        public string GetUptimeDisplay()
        {
            if (_monitoringService.IsRunning && _monitoringStartTime.HasValue)
            {
                var uptime = DateTime.Now - _monitoringStartTime.Value;
                return $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
            }
            return "00:00:00";
        }

        /// <summary>
        /// Checks if monitoring is currently active
        /// </summary>
        public bool IsMonitoring => _monitoringService.IsRunning;
    }
}
