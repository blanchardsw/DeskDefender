using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDefender.Interfaces;
using DeskDefender.Models.Configuration;
using DeskDefender.Models.Events;
using DeskDefender.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeskDefender
{
    /// <summary>
    /// Main window for the DeskDefender security monitoring application
    /// Implements comprehensive UI for monitoring system status and events
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MainWindow> _logger;
        private readonly IMonitorService _monitoringService;
        private readonly IEventLogger _eventLogger;
        private readonly IAlertService _alertService;
        private readonly EventCoordinatorService _eventCoordinator;
        private readonly EventDisplayService _eventDisplayService;
        private readonly AppSettings _settings;
        
        private readonly ObservableCollection<EventDisplayModel> _recentEvents;
        private readonly ObservableCollection<EventDisplayModel> _eventLog;
        private readonly DispatcherTimer _uiUpdateTimer;
        private DateTime _monitoringStartTime;
        private Bitmap _currentCameraFrame;
        private bool _isInitialized = false;

        public MainWindow(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            
            _logger = _serviceProvider.GetRequiredService<ILogger<MainWindow>>();
            _monitoringService = _serviceProvider.GetRequiredService<IMonitorService>();
            _eventLogger = _serviceProvider.GetRequiredService<IEventLogger>();
            _alertService = _serviceProvider.GetRequiredService<IAlertService>();
            _eventCoordinator = _serviceProvider.GetRequiredService<EventCoordinatorService>();
            _eventDisplayService = _serviceProvider.GetRequiredService<EventDisplayService>();
            _settings = _serviceProvider.GetRequiredService<AppSettings>();
            
            InitializeComponent();
            
            _recentEvents = new ObservableCollection<EventDisplayModel>();
            _eventLog = new ObservableCollection<EventDisplayModel>();
            
            RecentEventsList.ItemsSource = _recentEvents;
            EventLogList.ItemsSource = _eventLog;
            
            // Subscribe to event summaries for UI display
            _eventDisplayService.SummaryForUI += OnEventSummaryReceived;
            
            _uiUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uiUpdateTimer.Tick += (s, e) => UpdateUI();
            
            InitializeUI();
        }

        private void InitializeUI()
        {
            SystemInfoText.Text = $"System: {Environment.MachineName} | User: {Environment.UserName}";
            MotionSensitivitySlider.Value = _settings.MotionSensitivity;
            SensitivityValue.Text = _settings.MotionSensitivity.ToString("F1");
            _uiUpdateTimer.Start();
            _ = LoadEventLogAsync();
            
            // Event subscription is now handled by EventCoordinatorService
            // which was started during application initialization
            
            _isInitialized = true;
        }

        private void UpdateUI()
        {
            TimestampText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            bool isMonitoring = _monitoringService.IsRunning;
            if (isMonitoring)
            {
                var uptime = DateTime.UtcNow - _monitoringStartTime;
                UptimeText.Text = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
            }
        }

        private async void ToggleMonitoring_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_monitoringService.IsRunning)
                {
                    await Task.Run(() => _monitoringService.Stop());
                    StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                    StatusText.Text = "Monitoring Stopped";
                    ToggleMonitoringButton.Content = "Start Monitoring";
                }
                else
                {
                    await Task.Run(() => _monitoringService.Start());
                    _monitoringStartTime = DateTime.UtcNow;
                    StatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
                    StatusText.Text = "Monitoring Active";
                    ToggleMonitoringButton.Content = "Stop Monitoring";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling monitoring");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CaptureFrame_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_serviceProvider.GetService<ICameraService>() is ICameraService cameraService)
                {
                    var frame = await cameraService.CaptureFrameAsync();
                    if (frame != null)
                    {
                        _currentCameraFrame = cameraService.GetCurrentFrame();
                        UpdateCameraFeed(frame);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing frame");
            }
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCameraFrame == null) return;
            
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JPEG Image|*.jpg",
                FileName = $"capture_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.jpg"
            };
            
            if (saveDialog.ShowDialog() == true)
            {
                _currentCameraFrame.Save(saveDialog.FileName, System.Drawing.Imaging.ImageFormat.Jpeg);
            }
        }

        private void MotionSensitivity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;
            
            SensitivityValue.Text = e.NewValue.ToString("F1");
            if (_serviceProvider.GetService<ICameraService>() is ICameraService cameraService)
            {
                cameraService.SetMotionSensitivity(e.NewValue);
            }
        }

        private async void EventTypeFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized) await LoadEventLogAsync();
        }

        private async void SeverityFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized) await LoadEventLogAsync();
        }

        private async void RefreshLog_Click(object sender, RoutedEventArgs e)
        {
            await LoadEventLogAsync();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new DeskDefender.Windows.SettingsWindow(_serviceProvider)
                {
                    Owner = this
                };
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening settings window");
                System.Windows.MessageBox.Show(
                    "Failed to open settings window. Please try again.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();
        
        private void ViewEventLog_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Event log window not implemented in Phase 1", "Info");
        }
        
        private void ViewStatistics_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Statistics window not implemented in Phase 1", "Info");
        }
        
        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("DeskDefender v1.0\nSecurity Monitoring System", "About");
        }
        
        private async void TestAlert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _alertService.SendAlertAsync("Test alert from DeskDefender");
                MessageBox.Show("Test alert sent!", "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Alert failed: {ex.Message}", "Error");
            }
        }

        private void UpdateCameraFeed(Bitmap frame)
        {
            try
            {
                using var memory = new MemoryStream();
                frame.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;
                
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                
                CameraFeedImage.Source = bitmapImage;
                CameraPlaceholder.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating camera feed");
            }
        }

        private async Task LoadEventLogAsync()
        {
            try
            {
                var events = await _eventLogger.GetEventsAsync(DateTime.Today.AddDays(-7), DateTime.Now);
                
                _eventLog.Clear();
                foreach (var eventLog in events)
                {
                    _eventLog.Add(new EventDisplayModel(eventLog));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading event log");
            }
        }


        private void OnEventReceived(object sender, EventLog eventLog)
        {
            try
            {
                // Update UI on the UI thread
                Dispatcher.BeginInvoke(() =>
                {
                    var displayModel = new EventDisplayModel(eventLog);
                    
                    // Add to recent events (limit to 10 most recent)
                    _recentEvents.Insert(0, displayModel);
                    while (_recentEvents.Count > 10)
                    {
                        _recentEvents.RemoveAt(_recentEvents.Count - 1);
                    }
                    
                    // Add to full event log
                    _eventLog.Insert(0, displayModel);
                    
                    _logger.LogInformation("Event added to UI: {EventType} - {Description}", eventLog.EventType, eventLog.Description);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling received event");
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                // Stop monitoring service
                if (_monitoringService is DeskDefender.Services.CompositeMonitoringService compositeService)
                {
                    _logger.LogInformation("Stopping monitoring service");
                }
                
                if (_monitoringService.IsRunning)
                {
                    _monitoringService.Stop();
                }
                _uiUpdateTimer?.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during shutdown");
            }
            base.OnClosing(e);
        }
        
        #region Event Summary Handling
        
        private void OnEventSummaryReceived(EventSummary summary)
        {
            // Ensure UI updates happen on the UI thread
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Create an EventLog object for the EventDisplayModel constructor
                    var eventLog = new EventLog
                    {
                        Timestamp = summary.IntervalEnd,
                        EventType = "Event Summary",
                        Description = summary.GetSummaryDescription(),
                        Severity = DetermineEventSeverity(summary),
                        AlertSent = false
                    };

                    // Convert EventLog to EventDisplayModel for UI
                    var displayModel = new EventDisplayModel(eventLog);

                    // Add to recent events (limit to last 50)
                    _recentEvents.Insert(0, displayModel);
                    if (_recentEvents.Count > 50)
                    {
                        _recentEvents.RemoveAt(_recentEvents.Count - 1);
                    }

                    // Add to event log (limit to last 1000)
                    _eventLog.Insert(0, displayModel);
                    if (_eventLog.Count > 1000)
                    {
                        _eventLog.RemoveAt(_eventLog.Count - 1);
                    }

                    _logger.LogDebug("Event summary added to UI: {Description}", displayModel.Description);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error displaying event summary in UI");
                }
            });
        }

        private EventSeverity DetermineEventSeverity(EventSummary summary)
        {
            // Determine severity based on activity level
            bool hasKeyboardActivity = summary.KeyboardActivity?.KeystrokeCount > 0;
            bool hasMouseActivity = summary.MouseActivity?.ClickCount > 0 || summary.MouseActivity?.MovementEvents > 0;

            if (hasKeyboardActivity && hasMouseActivity)
            {
                return EventSeverity.Info; // Both activities present
            }
            else if (hasKeyboardActivity || hasMouseActivity)
            {
                return EventSeverity.Low; // Some activity
            }
            else
            {
                return EventSeverity.Low; // No significant activity
            }
        }
        
        #endregion
        
        #region Event Log Detail Modal
        
        private void EventLogList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (EventLogList.SelectedItem is EventDisplayModel selectedEvent)
                {
                    var detailWindow = new DeskDefender.Windows.LogDetailWindow(selectedEvent)
                    {
                        Owner = this
                    };
                    detailWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening event detail window");
                System.Windows.MessageBox.Show(
                    "Failed to open event details. Please try again.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Export logs to file
        /// </summary>
        private async void ExportLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|JSON files (*.json)|*.json|Text files (*.txt)|*.txt",
                    DefaultExt = "csv",
                    FileName = $"DeskDefender_Logs_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var exportService = _serviceProvider.GetRequiredService<LogExportService>();
                    await exportService.ExportLogsAsync(saveFileDialog.FileName);
                    
                    System.Windows.MessageBox.Show(
                        $"Logs exported successfully to:\n{saveFileDialog.FileName}",
                        "Export Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting logs");
                System.Windows.MessageBox.Show(
                    $"Failed to export logs: {ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        #endregion
        

    }

    public class EventDisplayModel
    {
        public string EventType { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public string TimeAgo => GetTimeAgo();
        public SolidColorBrush SeverityColor { get; set; }
        public bool AlertSent { get; set; }

        public EventDisplayModel(EventLog eventLog)
        {
            EventType = eventLog.EventType;
            Description = eventLog.Description;
            Timestamp = eventLog.Timestamp;
            AlertSent = eventLog.AlertSent;
            SeverityColor = GetSeverityBrush(eventLog.Severity);
        }

        private string GetTimeAgo()
        {
            var span = DateTime.UtcNow - Timestamp;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }

        private SolidColorBrush GetSeverityBrush(EventSeverity severity)
        {
            return severity switch
            {
                EventSeverity.Critical => new SolidColorBrush(Colors.Red),        // Red for critical
                EventSeverity.High => new SolidColorBrush(Colors.Red),             // Red for high
                EventSeverity.Medium => new SolidColorBrush(Colors.Yellow),        // Yellow for medium
                EventSeverity.Warning => new SolidColorBrush(Colors.Yellow),       // Yellow for warning
                EventSeverity.Low => new SolidColorBrush(Colors.Green),            // Green for low
                EventSeverity.Info => new SolidColorBrush(Colors.Gray),            // Gray for info
                _ => new SolidColorBrush(Colors.Gray)                              // Gray for unknown
            };
        }

        public void UpdateTimeAgo() { /* Trigger property change if needed */ }
    }
}