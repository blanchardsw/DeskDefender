using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
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
                _logger.LogInformation("Loading existing events from database at startup...");
                
                // Get events from the last 7 days to populate the UI
                var startDate = DateTime.UtcNow.AddDays(-7);
                var endDate = DateTime.UtcNow;
                
                var existingEvents = await _eventLogger.GetEventsAsync(startDate, endDate);
                _logger.LogInformation("Retrieved {EventCount} existing events from database", existingEvents.Count());
                
                // Clear existing UI events and populate with database events
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _eventLog.Clear();
                    _recentEvents.Clear();
                    
                    foreach (var eventLog in existingEvents.OrderByDescending(e => e.Timestamp))
                    {
                        var displayModel = new EventDisplayModel(eventLog);
                        _eventLog.Add(displayModel);
                        
                        // Add to recent events if it's within the last hour
                        if (eventLog.Timestamp > DateTime.UtcNow.AddHours(-1))
                        {
                            _recentEvents.Add(displayModel);
                        }
                    }
                    
                    _logger.LogInformation("Loaded {UIEventCount} events into UI, {RecentCount} recent events", 
                        _eventLog.Count, _recentEvents.Count);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load existing events from database at startup");
                // Don't throw - app should continue even if event loading fails
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

        /// <summary>
        /// Copy selected event to clipboard
        /// </summary>
        private void CopyEvent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (EventLogList.SelectedItem is EventDisplayModel selectedEvent)
                {
                    var eventText = $"[{selectedEvent.Timestamp:yyyy-MM-dd HH:mm:ss}] {selectedEvent.EventType} - {selectedEvent.Description}";
                    Clipboard.SetText(eventText);
                    _logger.LogDebug("Event copied to clipboard");
                }
                else
                {
                    System.Windows.MessageBox.Show("Please select an event to copy.", "No Event Selected", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying event to clipboard");
                System.Windows.MessageBox.Show($"Failed to copy event: {ex.Message}", "Copy Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Copy all events to clipboard
        /// </summary>
        private void CopyAllEvents_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_eventLog.Count == 0)
                {
                    System.Windows.MessageBox.Show("No events to copy.", "No Events", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var allEventsText = new StringBuilder();
                allEventsText.AppendLine("DeskDefender Event Log");
                allEventsText.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                allEventsText.AppendLine($"Total Events: {_eventLog.Count}");
                allEventsText.AppendLine(new string('=', 50));
                allEventsText.AppendLine();

                foreach (var eventItem in _eventLog.OrderBy(e => e.Timestamp))
                {
                    allEventsText.AppendLine($"[{eventItem.Timestamp:yyyy-MM-dd HH:mm:ss}] {eventItem.EventType} - {eventItem.Description}");
                }

                Clipboard.SetText(allEventsText.ToString());
                _logger.LogDebug("All events copied to clipboard");
                
                System.Windows.MessageBox.Show($"Copied {_eventLog.Count} events to clipboard.", "Copy Complete", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying all events to clipboard");
                System.Windows.MessageBox.Show($"Failed to copy events: {ex.Message}", "Copy Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Test database persistence by inserting and retrieving a mock event
        /// </summary>
        private async void TestDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Starting comprehensive database persistence test...");
                
                var message = new System.Text.StringBuilder();
                message.AppendLine("🔍 COMPREHENSIVE DATABASE PERSISTENCE TEST");
                message.AppendLine($"Test Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                message.AppendLine();
                
                // Check monitoring status
                message.AppendLine("📊 MONITORING STATUS:");
                message.AppendLine($"- Monitoring Active: {(_monitoringService != null ? "YES" : "NO")}");
                if (_monitoringService != null)
                {
                    // Add more monitoring details if available
                    message.AppendLine($"- Monitor Service Type: {_monitoringService.GetType().Name}");
                }
                message.AppendLine();
                
                // Create a test event
                var testEvent = new EventLog
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow,
                    EventType = "System",
                    Description = "Database Test Event - " + DateTime.Now.ToString("HH:mm:ss"),
                    Severity = EventSeverity.Info,
                    IsAlert = false,
                    Details = "This is a test event to verify database persistence",
                    Source = "DatabaseTest"
                };
                
                message.AppendLine("💾 DATABASE SAVE TEST:");

                // Save to database using the event logger service
                try
                {
                    await _eventLogger.LogEventAsync(testEvent);
                    message.AppendLine($"✅ Test event saved successfully (ID: {testEvent.Id})");
                    _logger.LogInformation("Test event saved to database with ID: {EventId}", testEvent.Id);
                }
                catch (Exception saveEx)
                {
                    message.AppendLine($"❌ SAVE FAILED: {saveEx.Message}");
                    message.AppendLine($"Exception Type: {saveEx.GetType().Name}");
                    if (saveEx.InnerException != null)
                    {
                        message.AppendLine($"Inner Exception: {saveEx.InnerException.Message}");
                    }
                    _logger.LogError(saveEx, "Failed to save test event to database");
                }
                
                message.AppendLine();

                // Wait a moment for the save to complete
                await Task.Delay(500);

                // Try to retrieve all events from database (last 30 days)
                message.AppendLine("🔍 DATABASE RETRIEVAL TEST:");
                try
                {
                    var startDate = DateTime.UtcNow.AddDays(-30);
                    var endDate = DateTime.UtcNow;
                    var allEvents = await _eventLogger.GetEventsAsync(startDate, endDate);
                    message.AppendLine($"✅ Retrieved {allEvents.Count()} events from database");
                    _logger.LogInformation("Retrieved {EventCount} events from database", allEvents.Count());

                    // Look for our test event
                    var foundTestEvent = allEvents.FirstOrDefault(e => e.Id == testEvent.Id);
                    message.AppendLine($"Test Event Found: {(foundTestEvent != null ? "✅ YES" : "❌ NO")}");
                    
                    if (foundTestEvent != null)
                    {
                        message.AppendLine($"Retrieved Event: {foundTestEvent.Description}");
                        message.AppendLine($"Event Type: {foundTestEvent.EventType}");
                        message.AppendLine($"Timestamp: {foundTestEvent.Timestamp}");
                    }

                    // Show recent events from database
                    message.AppendLine();
                    message.AppendLine("📋 RECENT EVENTS IN DATABASE:");
                    if (allEvents.Any())
                    {
                        var recentEvents = allEvents.OrderByDescending(e => e.Timestamp).Take(5);
                        foreach (var evt in recentEvents)
                        {
                            message.AppendLine($"- [{evt.Timestamp:HH:mm:ss}] {evt.EventType}: {evt.Description}");
                        }
                    }
                    else
                    {
                        message.AppendLine("❌ NO EVENTS FOUND IN DATABASE");
                        message.AppendLine("This explains why export is failing!");
                    }
                    
                    // Check UI event log count
                    message.AppendLine();
                    message.AppendLine("🖥️ UI EVENT LOG STATUS:");
                    message.AppendLine($"Events in UI: {_eventLog.Count}");
                    if (_eventLog.Count > 0)
                    {
                        message.AppendLine("Recent UI Events:");
                        foreach (var uiEvent in _eventLog.Take(3))
                        {
                            message.AppendLine($"- [{uiEvent.Timestamp:HH:mm:ss}] {uiEvent.EventType}: {uiEvent.Description}");
                        }
                    }

                    _logger.LogInformation("Database test completed. Found test event: {Found}", foundTestEvent != null);
                    
                    System.Windows.MessageBox.Show(message.ToString(), "Database Test Results", 
                        MessageBoxButton.OK, foundTestEvent != null ? MessageBoxImage.Information : MessageBoxImage.Warning);
                }
                catch (Exception retrieveEx)
                {
                    message.AppendLine($"❌ RETRIEVAL FAILED: {retrieveEx.Message}");
                    message.AppendLine($"Exception Type: {retrieveEx.GetType().Name}");
                    _logger.LogError(retrieveEx, "Failed to retrieve events from database");
                    
                    System.Windows.MessageBox.Show(message.ToString(), "Database Test Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database test failed");
                System.Windows.MessageBox.Show($"Database test failed: {ex.Message}\n\nStack trace:\n{ex.StackTrace}", 
                    "Database Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            EventType = eventLog.EventType ?? "Unknown";
            Description = eventLog.Description ?? "No description";
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
                EventSeverity.Medium or EventSeverity.Warning => new SolidColorBrush(Colors.Yellow), // Yellow for medium/warning
                EventSeverity.Low => new SolidColorBrush(Colors.Green),            // Green for low
                EventSeverity.Info => new SolidColorBrush(Colors.Gray),            // Gray for info
                _ => new SolidColorBrush(Colors.Gray)                              // Gray for unknown
            };
        }

        public void UpdateTimeAgo() { /* Trigger property change if needed */ }
    }
}