using System;
using System.Drawing;
using System.Windows;
using Microsoft.Extensions.Logging;
using DeskDefender.Interfaces;

namespace DeskDefender.Services
{
    /// <summary>
    /// Simple, reliable system tray service using explicit Windows Forms references
    /// Avoids namespace conflicts by using fully qualified names
    /// </summary>
    public class WorkingTrayService : ITrayService, IDisposable
    {
        private readonly ILogger<WorkingTrayService> _logger;
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private System.Windows.Forms.ContextMenuStrip? _contextMenu;
        private System.Windows.Forms.ToolStripMenuItem? _showMenuItem;
        private System.Windows.Forms.ToolStripMenuItem? _monitoringMenuItem;
        private System.Windows.Forms.ToolStripMenuItem? _exitMenuItem;
        private bool _disposed = false;
        private bool _isMonitoring = false;

        public event EventHandler? ShowMainWindow;
        public event EventHandler? ExitApplication;
        public event EventHandler? ToggleMonitoring;

        public bool IsVisible { get; private set; } = false;

        public WorkingTrayService(ILogger<WorkingTrayService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Initialize()
        {
            try
            {
                // Create the tray icon using fully qualified names to avoid conflicts
                _notifyIcon = new System.Windows.Forms.NotifyIcon
                {
                    Text = "DeskDefender - Security Monitoring",
                    Visible = false // Start hidden
                };

                // Create a simple icon
                UpdateTrayIcon();

                // Set up event handlers
                _notifyIcon.DoubleClick += (s, e) => ShowMainWindow?.Invoke(this, EventArgs.Empty);
                _notifyIcon.MouseClick += OnTrayIconClick;

                // Create context menu
                CreateContextMenu();

                _logger.LogInformation("Working tray service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize working tray service");
                throw;
            }
        }

        public void MinimizeToTray()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = true;
                    IsVisible = true;
                    _logger.LogDebug("Application minimized to tray - icon is now visible");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to minimize to tray");
            }
        }

        public void RestoreFromTray()
        {
            try
            {
                ShowMainWindow?.Invoke(this, EventArgs.Empty);
                _logger.LogDebug("Application restored from tray");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore from tray");
            }
        }

        public void ShowTrayNotification(string title, string message, int timeout = 3000)
        {
            try
            {
                if (_notifyIcon != null && IsVisible)
                {
                    _notifyIcon.ShowBalloonTip(timeout, title, message, System.Windows.Forms.ToolTipIcon.Info);
                    _logger.LogDebug("Tray notification shown: {Title} - {Message}", title, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show tray notification");
            }
        }

        public void UpdateMonitoringStatus(bool isMonitoring)
        {
            _isMonitoring = isMonitoring;
            UpdateTrayIcon();
            UpdateContextMenu();
            _logger.LogDebug("Monitoring status updated: {IsMonitoring}", isMonitoring);
        }

        private void CreateContextMenu()
        {
            try
            {
                _contextMenu = new System.Windows.Forms.ContextMenuStrip();

                // Show menu item
                _showMenuItem = new System.Windows.Forms.ToolStripMenuItem("Show DeskDefender");
                _showMenuItem.Click += (s, e) => ShowMainWindow?.Invoke(this, EventArgs.Empty);
                _contextMenu.Items.Add(_showMenuItem);

                _contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

                // Monitoring menu item
                _monitoringMenuItem = new System.Windows.Forms.ToolStripMenuItem();
                _monitoringMenuItem.Click += (s, e) => ToggleMonitoring?.Invoke(this, EventArgs.Empty);
                _contextMenu.Items.Add(_monitoringMenuItem);

                _contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

                // Exit menu item
                _exitMenuItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
                _exitMenuItem.Click += (s, e) => ExitApplication?.Invoke(this, EventArgs.Empty);
                _contextMenu.Items.Add(_exitMenuItem);

                // Assign context menu to notify icon
                if (_notifyIcon != null)
                {
                    _notifyIcon.ContextMenuStrip = _contextMenu;
                }

                UpdateContextMenu();
                _logger.LogDebug("Context menu created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create context menu");
                throw;
            }
        }

        private void UpdateContextMenu()
        {
            try
            {
                if (_monitoringMenuItem != null)
                {
                    _monitoringMenuItem.Text = _isMonitoring ? "Stop Monitoring" : "Start Monitoring";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update context menu");
            }
        }

        private void UpdateTrayIcon()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    // Create a simple colored icon based on monitoring status
                    var bitmap = new Bitmap(16, 16);
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        var color = _isMonitoring ? Color.LimeGreen : Color.Gray;
                        graphics.FillEllipse(new SolidBrush(color), 2, 2, 12, 12);
                    }

                    _notifyIcon.Icon = Icon.FromHandle(bitmap.GetHicon());
                    _notifyIcon.Text = $"DeskDefender - {(_isMonitoring ? "Monitoring Active" : "Monitoring Stopped")}";

                    _logger.LogDebug("Tray icon updated: {Status}", _isMonitoring ? "Active" : "Stopped");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update tray icon");
            }
        }

        private void OnTrayIconClick(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            try
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    // Left-click shows main window
                    ShowMainWindow?.Invoke(this, EventArgs.Empty);
                    _logger.LogDebug("Tray icon left-clicked - showing main window");
                }
                // Right-click shows context menu (handled automatically by ContextMenuStrip)
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tray icon click");
            }
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
                    try
                    {
                        if (_notifyIcon != null)
                        {
                            _notifyIcon.Visible = false;
                            _notifyIcon.Dispose();
                        }

                        _contextMenu?.Dispose();

                        _logger.LogDebug("Working tray service disposed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during working tray service disposal");
                    }
                }
                _disposed = true;
            }
        }

        ~WorkingTrayService()
        {
            Dispose(false);
        }
    }
}
