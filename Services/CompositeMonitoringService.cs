using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using DeskDefender.Models.Configuration;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Composite monitoring service that coordinates all monitoring components
    /// Implements the Composite pattern to manage multiple monitoring services
    /// Uses the Facade pattern to provide a simplified interface to complex subsystems
    /// Employs the Observer pattern to handle events from multiple sources
    /// </summary>
    public class CompositeMonitoringService : IMonitorService
    {
        #region Private Fields

        private readonly ILogger<CompositeMonitoringService> _logger;
        private readonly IInputMonitor _inputMonitor;
        private readonly ICameraService _cameraService;
        private readonly IEventLogger _eventLogger;
        private readonly IAlertService _alertService;
        private readonly AppSettings _settings;
        
        // Monitoring state
        private bool _isMonitoring = false;
        private readonly object _lockObject = new object();
        
        // Event aggregation for intelligent alerting
        private readonly List<EventLog> _recentEvents = new List<EventLog>();
        private readonly TimeSpan _eventAggregationWindow = TimeSpan.FromMinutes(5);
        private DateTime _lastEventCleanup = DateTime.UtcNow;

        // Image storage management
        private readonly string _imageStoragePath;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the CompositeMonitoringService
        /// Uses dependency injection to receive all required services
        /// </summary>
        /// <param name="inputMonitor">Input monitoring service</param>
        /// <param name="cameraService">Camera monitoring service</param>
        /// <param name="eventLogger">Event logging service</param>
        /// <param name="alertService">Alert delivery service</param>
        /// <param name="settings">Application settings</param>
        /// <param name="logger">Logger instance</param>
        public CompositeMonitoringService(
            IInputMonitor inputMonitor,
            ICameraService cameraService,
            IEventLogger eventLogger,
            IAlertService alertService,
            AppSettings settings,
            ILogger<CompositeMonitoringService> logger)
        {
            _inputMonitor = inputMonitor ?? throw new ArgumentNullException(nameof(inputMonitor));
            _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
            _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Setup image storage directory
            _imageStoragePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settings.ImageStoragePath);
            Directory.CreateDirectory(_imageStoragePath);

            // Subscribe to events from monitoring services
            SubscribeToEvents();

            _logger.LogInformation("CompositeMonitoringService initialized");
        }

        #endregion

        #region IMonitorService Implementation

        /// <summary>
        /// Gets the current status of the monitoring service
        /// </summary>
        public bool IsRunning => _isMonitoring;

        /// <summary>
        /// Event fired when the service status changes
        /// </summary>
        public event EventHandler<bool> StatusChanged;

        /// <summary>
        /// Starts all monitoring services in a coordinated manner
        /// Implements the Template Method pattern with comprehensive error handling
        /// </summary>
        public void Start()
        {
            lock (_lockObject)
            {
                if (_isMonitoring)
                {
                    _logger.LogWarning("Monitoring is already running");
                    return;
                }

                try
                {
                    _logger.LogInformation("Starting comprehensive monitoring system...");

                    // Start services in order of dependency
                    var startupTasks = new List<Task>();
                    var serviceStartResults = new List<string>();

                    // Start input monitoring if enabled
                    if (_settings.EnableInputMonitoring)
                    {
                        startupTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                _inputMonitor.SetSensitivity(_settings.InputSensitivityThreshold);
                                _inputMonitor.Start();
                                _logger.LogInformation("Input monitoring started successfully");
                                lock (serviceStartResults) { serviceStartResults.Add("Input monitoring: Started"); }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to start input monitoring, but continuing with other services");
                                lock (serviceStartResults) { serviceStartResults.Add("Input monitoring: Failed"); }
                            }
                        }));
                    }

                    // Start camera monitoring if enabled
                    if (_settings.EnableMotionDetection)
                    {
                        startupTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                _cameraService.SetMotionSensitivity(_settings.MotionSensitivity);
                                _cameraService.Start();
                                if (_cameraService.IsRunning)
                                {
                                    _logger.LogInformation("Camera monitoring started successfully");
                                    lock (serviceStartResults) { serviceStartResults.Add("Camera monitoring: Started"); }
                                }
                                else
                                {
                                    _logger.LogWarning("Camera monitoring failed to start (camera not available)");
                                    lock (serviceStartResults) { serviceStartResults.Add("Camera monitoring: Not available"); }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to start camera monitoring, but continuing with other services");
                                lock (serviceStartResults) { serviceStartResults.Add("Camera monitoring: Failed"); }
                            }
                        }));
                    }

                    // Wait for all services to attempt startup (don't fail if individual services fail)
                    try
                    {
                        Task.WaitAll(startupTasks.ToArray(), TimeSpan.FromSeconds(10));
                    }
                    catch (AggregateException ex)
                    {
                        _logger.LogWarning("Some services failed to start, but monitoring will continue with available services: {Exception}", ex.Message);
                    }

                    _isMonitoring = true;
                    StatusChanged?.Invoke(this, true);
                    
                    // Log which services started successfully
                    var successMessage = "Monitoring started with the following services: " + string.Join(", ", serviceStartResults);
                    _logger.LogInformation(successMessage);

                    // Log startup event
                    var startupEvent = new EventLog
                    {
                        EventType = "System",
                        Description = "DeskDefender monitoring started",
                        Severity = EventSeverity.Info,
                        Source = "CompositeMonitoringService"
                    };
                    
                    // Log startup event outside of lock to avoid async/await in lock
                    Task.Run(async () => await _eventLogger.LogAsync(startupEvent));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start monitoring services");
                    
                    // Attempt cleanup on failure
                    try
                    {
                        Stop();
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(cleanupEx, "Error during cleanup after startup failure");
                    }
                    
                    throw;
                }
            }
        }

        /// <summary>
        /// Stops all monitoring services in a coordinated manner
        /// Ensures proper cleanup and resource disposal
        /// </summary>
        public void Stop()
        {
            lock (_lockObject)
            {
                if (!_isMonitoring)
                {
                    return;
                }

                try
                {
                    _logger.LogInformation("Stopping monitoring services...");

                    // Stop services in reverse order
                    var stopTasks = new List<Task>();

                    if (_cameraService.IsRunning)
                    {
                        stopTasks.Add(Task.Run(() =>
                        {
                            _cameraService.Stop();
                            _logger.LogInformation("Camera monitoring stopped");
                        }));
                    }

                    if (_inputMonitor.IsRunning)
                    {
                        stopTasks.Add(Task.Run(() =>
                        {
                            _inputMonitor.Stop();
                            _logger.LogInformation("Input monitoring stopped");
                        }));
                    }

                    // Wait for all services to stop
                    Task.WaitAll(stopTasks.ToArray(), TimeSpan.FromSeconds(5));

                    // No flush method needed - async operations handle this

                    _isMonitoring = false;
                    StatusChanged?.Invoke(this, false);
                    _logger.LogInformation("All monitoring services stopped");

                    // Log shutdown event outside of lock to avoid async/await in lock
                    Task.Run(async () => await _eventLogger.LogAsync(new EventLog
                    {
                        EventType = "System",
                        Description = "Monitoring services stopped",
                        Severity = EventSeverity.Info,
                        Timestamp = DateTime.UtcNow,
                        Source = "CompositeMonitoringService"
                    }));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while stopping monitoring services");
                }
            }
        }

        /// <summary>
        /// Gets the current monitoring status
        /// </summary>
        public bool IsMonitoring => _isMonitoring;

        #endregion

        #region Event Handling

        /// <summary>
        /// Subscribes to events from all monitoring services
        /// Implements the Observer pattern for event coordination
        /// </summary>
        private void SubscribeToEvents()
        {
            // Subscribe to input events
            _inputMonitor.InputDetected += OnInputDetected;

            // Subscribe to camera events
            _cameraService.MotionDetected += OnMotionDetected;
            _cameraService.FrameCaptured += OnFrameCaptured;

            _logger.LogDebug("Event subscriptions established");
        }

        /// <summary>
        /// Logs an event asynchronously (wrapper for ProcessEventAsync)
        /// </summary>
        private async Task LogEventAsync(EventLog eventLog)
        {
            await ProcessEventAsync(eventLog);
        }

        /// <summary>
        /// Processes events from monitoring services
        /// Implements the Template Method pattern with enrichment and alerting
        /// </summary>
        private async Task ProcessEventAsync(EventLog eventLog)
        {
            try
            {
                // Enrich event with additional metadata
                eventLog.Timestamp = DateTime.UtcNow;
                
                // Log the event to database
                await _eventLogger.LogAsync(eventLog);
                
                _logger.LogDebug("Event processed and logged: {EventType} - {Description}", eventLog.EventType, eventLog.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event: {EventType}", eventLog.EventType);
            }
        }

        /// <summary>
        /// Handles motion detection events from camera service
        /// Implements intelligent event processing and alerting logic
        /// </summary>
        private async void OnMotionDetected(object sender, MotionDetectedEventArgs motionArgs)
        {
            try
            {
                // Create camera event from motion detection
                var cameraEvent = new CameraEvent
                {
                    Id = Guid.NewGuid(),
                    Timestamp = motionArgs.Timestamp,
                    EventType = "Motion",
                    Description = $"Motion detected with {motionArgs.MotionLevel:F2}% change",
                    Severity = motionArgs.MotionLevel > 50 ? EventSeverity.Critical : EventSeverity.Warning,
                    CameraId = "0",
                    DetectionType = CameraDetectionType.Motion,
                    Source = "OpenCvCameraService"
                };

                // Process the event
                await LogEventAsync(cameraEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing motion detection event");
            }
        }



        /// <summary>
        /// Handles input detection events
        /// Implements intelligent event processing and alerting logic
        /// </summary>
        private async void OnInputDetected(object sender, InputEvent inputEvent)
        {
            try
            {
                _logger.LogDebug("Input event received: {Description}", inputEvent.Description);

                // Process and enrich the event
                await ProcessEventAsync(inputEvent);

                // Determine if this event should trigger an alert
                if (ShouldTriggerAlert(inputEvent))
                {
                    await TriggerAlert(inputEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing input event");
            }
        }

        /// <summary>
        /// Handles motion detection events from camera
        /// Coordinates image capture with motion detection
        /// </summary>
        private async void OnMotionDetected(object sender, CameraEvent cameraEvent)
        {
            try
            {
                _logger.LogDebug("Motion event received: {Description}", cameraEvent.Description);

                // Process and enrich the event
                await ProcessEvent(cameraEvent);

                // Determine if this event should trigger an alert
                if (ShouldTriggerAlert(cameraEvent))
                {
                    await TriggerAlert(cameraEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing motion event");
            }
        }

        /// <summary>
        /// Handles frame capture events from camera
        /// Manages image storage and cleanup
        /// </summary>
        private async void OnFrameCaptured(object sender, System.Drawing.Bitmap frame)
        {
            try
            {
                if (_settings.CapturePhotos && frame != null)
                {
                    // Save frame to storage
                    var imagePath = await SaveCapturedFrame(frame);
                    _logger.LogDebug("Frame captured and saved: {ImagePath}", imagePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing captured frame");
            }
        }

        #endregion

        #region Event Processing

        /// <summary>
        /// Processes and enriches security events
        /// Implements the Chain of Responsibility pattern for event processing
        /// </summary>
        /// <param name="eventLog">Event to process</param>
        private async Task ProcessEvent(EventLog eventLog)
        {
            try
            {
                // Add event to recent events for correlation
                lock (_recentEvents)
                {
                    _recentEvents.Add(eventLog);
                    CleanupOldEvents();
                }

                // Enrich event with additional context
                EnrichEvent(eventLog);

                // Log the event
                await _eventLogger.LogAsync(eventLog);

                _logger.LogDebug("Event processed and logged: {EventId}", eventLog.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event: {EventId}", eventLog.Id);
            }
        }

        /// <summary>
        /// Enriches events with additional contextual information
        /// Implements the Decorator pattern for event enhancement
        /// </summary>
        /// <param name="eventLog">Event to enrich</param>
        private void EnrichEvent(EventLog eventLog)
        {
            try
            {
                // Add system context
                var metadata = new
                {
                    MachineName = Environment.MachineName,
                    UserName = Environment.UserName,
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSet = Environment.WorkingSet,
                    TickCount = Environment.TickCount64,
                    RecentEventCount = _recentEvents.Count
                };

                eventLog.Metadata = System.Text.Json.JsonSerializer.Serialize(metadata);
                eventLog.Source = eventLog.Source ?? "CompositeMonitoringService";

                _logger.LogDebug("Event enriched with system context: {EventId}", eventLog.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching event: {EventId}", eventLog.Id);
            }
        }

        /// <summary>
        /// Determines if an event should trigger an alert
        /// Implements business logic for intelligent alerting
        /// Uses event correlation and severity analysis
        /// </summary>
        /// <param name="eventLog">Event to evaluate</param>
        /// <returns>True if alert should be triggered</returns>
        private bool ShouldTriggerAlert(EventLog eventLog)
        {
            try
            {
                // Always alert on high severity events
                if (eventLog.Severity >= EventSeverity.High)
                {
                    return true;
                }

                // Check for event patterns that indicate suspicious activity
                lock (_recentEvents)
                {
                    var recentSimilarEvents = _recentEvents.FindAll(e => 
                        e.EventType == eventLog.EventType && 
                        DateTime.UtcNow - e.Timestamp < TimeSpan.FromMinutes(10));

                    // Alert if multiple similar events in short time
                    if (recentSimilarEvents.Count >= 3)
                    {
                        _logger.LogInformation("Multiple similar events detected, triggering alert");
                        return true;
                    }
                }

                // Alert on medium severity events during off-hours
                if (eventLog.Severity >= EventSeverity.Medium)
                {
                    var currentHour = DateTime.Now.Hour;
                    if (currentHour < 6 || currentHour > 22) // Outside business hours
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating alert criteria for event: {EventId}", eventLog.Id);
                return false;
            }
        }

        /// <summary>
        /// Triggers an alert for a security event
        /// Implements the Command pattern for alert execution
        /// </summary>
        /// <param name="eventLog">Event to alert about</param>
        private async Task TriggerAlert(EventLog eventLog)
        {
            try
            {
                // Send alert via configured service
                await _alertService.SendAlertAsync(eventLog.Description, eventLog.ImagePath);

                // Mark event as alerted
                eventLog.AlertSent = true;

                _logger.LogInformation("Alert triggered for event: {EventType} - {EventId}", 
                    eventLog.EventType, eventLog.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering alert for event: {EventId}", eventLog.Id);
            }
        }

        #endregion

        #region Image Management

        /// <summary>
        /// Saves a captured frame to storage
        /// Implements file naming conventions and storage management
        /// </summary>
        /// <param name="frame">Bitmap frame to save</param>
        /// <returns>Path to saved image file</returns>
        private async Task<string> SaveCapturedFrame(System.Drawing.Bitmap frame)
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss-fff");
                var fileName = $"capture_{timestamp}.jpg";
                var filePath = Path.Combine(_imageStoragePath, fileName);

                // Save image asynchronously
                await Task.Run(() =>
                {
                    frame.Save(filePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                });

                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving captured frame");
                return null;
            }
        }

        #endregion

        #region Cleanup and Maintenance

        /// <summary>
        /// Cleans up old events from the recent events cache
        /// Implements memory management for long-running processes
        /// </summary>
        private void CleanupOldEvents()
        {
            if (DateTime.UtcNow - _lastEventCleanup < TimeSpan.FromMinutes(1))
            {
                return; // Don't cleanup too frequently
            }

            var cutoffTime = DateTime.UtcNow - _eventAggregationWindow;
            _recentEvents.RemoveAll(e => e.Timestamp < cutoffTime);
            _lastEventCleanup = DateTime.UtcNow;

            _logger.LogDebug("Cleaned up old events, {Count} events remaining", _recentEvents.Count);
        }

        /// <summary>
        /// Performs periodic maintenance tasks
        /// Can be called by a timer or scheduled task
        /// </summary>
        public async Task PerformMaintenance()
        {
            try
            {
                _logger.LogDebug("Performing maintenance tasks...");

                // Cleanup old events
                lock (_recentEvents)
                {
                    CleanupOldEvents();
                }

                // Cleanup old log entries if configured
                if (_settings.LogRetentionDays > 0)
                {
                    var cutoffDate = DateTime.UtcNow.AddDays(-_settings.LogRetentionDays);
                    await _eventLogger.ClearOldEventsAsync(cutoffDate);
                    _logger.LogInformation("Cleaned up old log entries older than {CutoffDate}", cutoffDate);
                }

                _logger.LogDebug("Maintenance tasks completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during maintenance");
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Ensures proper cleanup of all resources
        /// Implements the Dispose pattern for deterministic resource management
        /// </summary>
        public void Dispose()
        {
            try
            {
                Stop();

                // Unsubscribe from events
                _inputMonitor.InputDetected -= OnInputDetected;
                _cameraService.MotionDetected -= OnMotionDetected;
                _cameraService.FrameCaptured -= OnFrameCaptured;

                _logger.LogInformation("CompositeMonitoringService disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
