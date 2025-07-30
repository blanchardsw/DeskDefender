using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskDefender.Controllers;
using DeskDefender.Interfaces;
using DeskDefender.Models.Configuration;
using DeskDefender.Models.Events;
using DeskDefender.Services;
using DeskDefender.Utils;
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
        
        // Phase 2: Session Lock Detection & Background Monitoring Services
        private readonly ISessionMonitor _sessionMonitor;
        private readonly ITrayService _trayService;
        private readonly IBackgroundMonitoringService _backgroundMonitoringService;
        
        private readonly ObservableCollection<EventDisplayModel> _recentEvents;
        private readonly ObservableCollection<EventDisplayModel> _eventLog;
        private readonly DispatcherTimer _uiUpdateTimer;
        private bool _isExiting = false; // Flag to distinguish Exit menu from X button
        private bool _isInitialized = false;
        private Bitmap? _currentCameraFrame;
        private DispatcherTimer? _eventRefreshTimer;
        
        // Refactored: Controllers for better separation of concerns
        private readonly EventDisplayController _eventDisplayController;
        private readonly MonitoringController _monitoringController;
        private readonly SessionController _sessionController;

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
            
            // Phase 2: Initialize session lock detection & background monitoring services
            _sessionMonitor = _serviceProvider.GetRequiredService<ISessionMonitor>();
            _trayService = _serviceProvider.GetRequiredService<ITrayService>();
            _backgroundMonitoringService = _serviceProvider.GetRequiredService<IBackgroundMonitoringService>();
            
            InitializeComponent();
            
            _recentEvents = new ObservableCollection<EventDisplayModel>();
            _eventLog = new ObservableCollection<EventDisplayModel>();
            
            RecentEventsList.ItemsSource = _recentEvents;
            EventLogList.ItemsSource = _eventLog;
            
            // Initialize controllers for better separation of concerns
            _eventDisplayController = new EventDisplayController(
                _serviceProvider.GetRequiredService<ILogger<EventDisplayController>>(),
                _eventLogger,
                _recentEvents,
                _eventLog,
                Dispatcher);
                
            _monitoringController = new MonitoringController(
                _serviceProvider.GetRequiredService<ILogger<MonitoringController>>(),
                _monitoringService,
                _backgroundMonitoringService,
                _trayService,
                Dispatcher);
                
            _sessionController = new SessionController(
                _serviceProvider.GetRequiredService<ILogger<SessionController>>(),
                _sessionMonitor,
                _trayService,
                Dispatcher);
            
            // Subscribe to event summaries for UI display
            _eventDisplayService.SummaryForUI += OnEventSummaryReceived;
            
            // Initialize UI update timer
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _uiUpdateTimer.Tick += UpdateUI;
            _uiUpdateTimer.Start();
            
            // Initialize event refresh timer for real-time event list updates
            _eventRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5) // Refresh every 5 seconds
            };
            _eventRefreshTimer.Tick += RefreshEventList;
            _eventRefreshTimer.Start();
            
            InitializeUI();
        }

        private void InitializeUI()
        {
            SystemInfoText.Text = $"System: {Environment.MachineName} | User: {Environment.UserName}";
            MotionSensitivitySlider.Value = _settings.MotionSensitivity;
            SensitivityValue.Text = _settings.MotionSensitivity.ToString("F1");
            _uiUpdateTimer.Start();
            
            // Use EventDisplayController to load events
            _ = _eventDisplayController.LoadEventLogAsync();
            
            // Event subscription is now handled by EventCoordinatorService
            // which was started during application initialization
            
            // Phase 2: Initialize session monitoring and tray services using controllers
            InitializePhase2Services();
            _sessionController.Initialize();
            
            _isInitialized = true;
        }

        /// <summary>
        /// Initializes Phase 2 services: session monitoring, tray integration, and background monitoring
        /// </summary>
        private void InitializePhase2Services()
        {
            try
            {
                _logger.LogInformation("Initializing Phase 2 services: session monitoring, tray, and background monitoring");

                // Subscribe to tray service events
                _trayService.ShowMainWindow += OnTrayShowMainWindow;
                _trayService.ExitApplication += OnTrayExitApplication;
                _trayService.ToggleMonitoring += OnTrayToggleMonitoring;

                // Subscribe to background monitoring status changes
                _backgroundMonitoringService.StatusChanged += OnBackgroundMonitoringStatusChanged;

                // Initialize tray service
                _trayService.Initialize();

                // DO NOT start monitoring automatically - let user control when to start
                // _backgroundMonitoringService.StartBackgroundMonitoring(); // Removed automatic startup

                // Update tray with initial monitoring status (should be stopped)
                _trayService.UpdateMonitoringStatus(_monitoringService.IsRunning);

                // Update UI to reflect initial monitoring status (should be stopped)
                _monitoringController.UpdateUIForMonitoringState(
                    _monitoringService.IsRunning,
                    ToggleMonitoringButton,
                    StatusIndicator,
                    StatusText);

                // Handle window state changes for minimize to tray
                this.StateChanged += OnWindowStateChanged;
                this.Closing += OnWindowClosing;

                _logger.LogInformation("Phase 2 services initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Phase 2 services");
                // Log error silently for stealth operation - no MessageBox
                _logger.LogError(ex, "Error initializing background monitoring - continuing silently");
            }
        }

        private void UpdateUI(object sender, EventArgs e)
        {
            TimestampText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            bool isMonitoring = _monitoringService.IsRunning;
            UptimeText.Text = _monitoringController.GetUptimeDisplay();
        }

        /// <summary>
        /// Handles monitoring toggle using the MonitoringController - refactored for better separation of concerns
        /// </summary>
        private async void ToggleMonitoring_Click(object sender, RoutedEventArgs e)
        {
            await _monitoringController.ToggleMonitoringAsync(
                ToggleMonitoringButton,
                StatusIndicator,
                StatusText);
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
            if (_isInitialized) ApplyEventFilters();
        }
        
        private void ShowSystemEventsToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) ApplyEventFilters();
        }

        private async void SeverityFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized) ApplyEventFilters();
        }

        private void TimeRangeValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitialized) ValidateAndApplyTimeFilter();
        }

        private void TimeRangeUnit_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized) ValidateAndApplyTimeFilter();
        }

        private void ValidateAndApplyTimeFilter()
        {
            try
            {
                if (int.TryParse(TimeRangeValue.Text, out int value))
                {
                    var selectedUnit = (TimeRangeUnit.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    
                    // Apply range limits
                    if (selectedUnit == "Hours" && value > 24)
                    {
                        TimeRangeValue.Text = "24";
                        return;
                    }
                    else if (selectedUnit == "Days" && value > 7)
                    {
                        TimeRangeValue.Text = "7";
                        return;
                    }
                    else if (value < 1)
                    {
                        TimeRangeValue.Text = "1";
                        return;
                    }
                }
                else
                {
                    // Invalid input, reset to 1
                    TimeRangeValue.Text = "1";
                    return;
                }
                
                ApplyEventFilters();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating time filter");
            }
        }

        /// <summary>
        /// Applies event filters using the EventDisplayController - refactored for better separation of concerns
        /// </summary>
        private void ApplyEventFilters()
        {
            if (!_isInitialized || EventTypeFilter == null || SeverityFilter == null || TimeRangeValue == null || TimeRangeUnit == null || ShowSystemEventsToggle == null)
                return;
                
            try
            {
                var selectedEventType = UIHelper.GetComboBoxItemContent(EventTypeFilter.SelectedItem);
                var selectedSeverity = UIHelper.GetComboBoxItemContent(SeverityFilter.SelectedItem);
                bool showSystemEvents = ShowSystemEventsToggle.IsChecked ?? true;
                
                if (int.TryParse(TimeRangeValue.Text, out int timeValue))
                {
                    var timeUnit = UIHelper.GetComboBoxItemContent(TimeRangeUnit.SelectedItem) ?? "Hours";
                    
                    _eventDisplayController.ApplyEventFilters(
                        selectedEventType,
                        selectedSeverity,
                        timeValue,
                        timeUnit,
                        showSystemEvents,
                        EventLogList);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ApplyEventFilters");
                EventLogList.ItemsSource = _eventLog;
            }
        }

        private string GetSeverityFromColor(SolidColorBrush brush)
        {
            if (brush == null) return "Unknown";
            
            var color = brush.Color;
            if (color == System.Windows.Media.Colors.Red) return "Critical";
            if (color == System.Windows.Media.Colors.Orange) return "High";
            if (color == System.Windows.Media.Colors.Yellow) return "Medium";
            if (color == System.Windows.Media.Colors.Green) return "Low";
            if (color == System.Windows.Media.Colors.Gray) return "Info";
            
            return "Unknown";
        }

        /// <summary>
        /// Auto-refresh event handler called by timer to update Event Log without manual refresh
        /// </summary>
        private async void RefreshEventList(object? sender, EventArgs e)
        {
            try
            {
                await LoadEventLogAsync();
                _logger.LogDebug("Event Log auto-refreshed - {Count} events loaded", _eventLog.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto-refresh of Event Log");
            }
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

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _isExiting = true; // Set flag to indicate intentional exit
            Close();
        }
        
        private void ViewEventLog_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Event log window not implemented in Phase 1", "Info");
        }
        
        private void ViewStatistics_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Statistics window not implemented in Phase 1", "Info");
        }
        
        private void About_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("DeskDefender v1.0\nSecurity Monitoring System", "About");
        }
        
        private async void TestAlert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _alertService.SendAlertAsync("Test alert from DeskDefender");
                _logger.LogInformation("Test alert sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test alert failed: {Message}", ex.Message);
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
                var startDate = DateTime.Now.AddDays(-7);
                var endDate = DateTime.Now;
                
                var existingEvents = await _eventLogger.GetEventsAsync(startDate, endDate);
                _logger.LogInformation("Retrieved {EventCount} existing events from database", existingEvents.Count());
                
                // Clear existing UI events and populate with database events
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _eventLog.Clear();
                    _recentEvents.Clear();
                    
                    foreach (var eventLog in existingEvents.OrderByDescending(e => e.Timestamp))
                    {
                        var displayModel = new EventDisplayModel(eventLog);
                        _eventLog.Add(displayModel);
                        
                        // Add to recent events if it's within the last hour
                        if (eventLog.Timestamp > DateTime.Now.AddHours(-1))
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


        /// <summary>
        /// Handles incoming events using the EventDisplayController - refactored for better separation of concerns
        /// </summary>
        private void OnEventReceived(object sender, EventLog eventLog)
        {
            _eventDisplayController.HandleEventReceived(eventLog);
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
                    System.Windows.Clipboard.SetText(eventText);
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

                System.Windows.Clipboard.SetText(allEventsText.ToString());
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
                    Timestamp = DateTime.Now,
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
                    var startDate = DateTime.Now.AddDays(-30);
                    var endDate = DateTime.Now;
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
        
        #region Phase 2: Session Lock Detection & Background Monitoring Event Handlers

        /// <summary>
        /// Handles tray service request to show main window
        /// </summary>
        private void OnTrayShowMainWindow(object? sender, EventArgs e)
        {
            try
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
                _logger.LogDebug("Main window restored from system tray");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring window from tray");
            }
        }

        /// <summary>
        /// Handles tray service request to exit application
        /// </summary>
        private void OnTrayExitApplication(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("Exit requested from system tray");
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exiting application from tray");
            }
        }

        /// <summary>
        /// Handles tray service request to toggle monitoring
        /// </summary>
        private void OnTrayToggleMonitoring(object? sender, EventArgs e)
        {
            try
            {
                // Use the existing toggle monitoring logic
                ToggleMonitoring_Click(sender, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling monitoring from tray");
            }
        }

        /// <summary>
        /// Handles session state changes from the session monitor
        /// </summary>
        private void OnSessionStateChanged(object? sender, SessionStateChangedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
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
                    
                    // You could add a session status label to the UI to show this
                    // For now, we'll just log it
                    _logger.LogDebug("UI updated for session state: {Status}", sessionStatus);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling session state change in UI");
            }
        }

        /// <summary>
        /// Handles background monitoring status changes
        /// </summary>
        private void OnBackgroundMonitoringStatusChanged(object? sender, BackgroundMonitoringStatusEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    _logger.LogDebug("Background monitoring status changed: {ActiveServices}/{TotalServices} services active", 
                        e.Status.ActiveServicesCount, 4);
                    
                    // Show tray notification for critical service failures
                    if (!e.Status.AllCriticalServicesActive)
                    {
                        _trayService.ShowTrayNotification(
                            "DeskDefender Warning", 
                            "Some monitoring services are not active. Check the application.",
                            5000);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling background monitoring status change");
            }
        }

        /// <summary>
        /// Handles window state changes for minimize to tray functionality
        /// </summary>
        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            try
            {
                if (this.WindowState == WindowState.Minimized)
                {
                    this.Hide();
                    _trayService.MinimizeToTray();
                    _logger.LogDebug("Window minimized to system tray");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling window state change");
            }
        }

        /// <summary>
        /// Handles window closing event to minimize to tray instead of closing
        /// </summary>
        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_isExiting)
                {
                    // User clicked Exit menu - actually exit the application
                    _logger.LogInformation("Application exiting via Exit menu");
                    
                    // Stop monitoring services before exit
                    if (_monitoringService.IsRunning)
                    {
                        _monitoringService.Stop();
                    }
                    
                    // Allow the application to close
                    return;
                }
                
                // User clicked X button - exit the application for stealth operation
                _logger.LogInformation("Application exiting via X button");
                
                // Stop monitoring services before exit
                if (_monitoringService.IsRunning)
                {
                    _monitoringService.Stop();
                }
                
                // Allow the application to close
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling window closing");
            }
        }

        #endregion

        #region Event List Auto-Refresh
        

        
        #endregion

        #region Menu Event Handlers

        /// <summary>
        /// Handles the Alert Settings menu click event
        /// </summary>
        private void AlertSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Opening Alert Settings window");
                
                var alertSettingsWindow = new Windows.AlertSettingsWindow(
                    _serviceProvider.GetRequiredService<ISettingsService>(),
                    _alertService,
                    _serviceProvider.GetRequiredService<ILogger<Windows.AlertSettingsWindow>>());
                
                alertSettingsWindow.Owner = this;
                var result = alertSettingsWindow.ShowDialog();
                
                if (result == true)
                {
                    _logger.LogInformation("Alert settings saved successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening Alert Settings window");
                System.Windows.MessageBox.Show(
                    $"Error opening Alert Settings: {ex.Message}",
                    "Alert Settings Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles the Clear Logs menu click event
        /// </summary>
        private async void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("User requested to clear all logs from database");
                
                // Use the event logger to clear all events directly
                if (_eventLogger is SqliteEventLogger sqliteLogger)
                {
                    await sqliteLogger.ClearAllEventsAsync();
                    _logger.LogInformation("All logs cleared from database successfully");
                    
                    // Refresh the event log display
                    await LoadEventLogAsync();
                }
                else
                {
                    _logger.LogError("Event logger is not SqliteEventLogger, cannot clear logs directly");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing logs from database");
                System.Windows.MessageBox.Show(
                    $"Error clearing logs: {ex.Message}",
                    "Clear Logs Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

    }

    public class EventDisplayModel : INotifyPropertyChanged
    {
        public string EventType { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public bool AlertSent { get; set; }
        public System.Windows.Media.Brush SeverityColor { get; set; }
        
        private string _timeAgo;
        public string TimeAgo
        {
            get => _timeAgo;
            set
            {
                _timeAgo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimeAgo)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public EventDisplayModel(EventLog eventLog)
        {
            EventType = eventLog.EventType ?? "Unknown";
            Description = eventLog.Description ?? "No description";
            Timestamp = eventLog.Timestamp;
            AlertSent = eventLog.AlertSent;
            SeverityColor = Utils.UIHelper.GetSeverityBrush(eventLog.Severity);
            _timeAgo = Utils.UIHelper.GetTimeAgo(Timestamp);
        }

        public void UpdateTimeAgo()
        {
            TimeAgo = Utils.UIHelper.GetTimeAgo(Timestamp);
        }
    }
}