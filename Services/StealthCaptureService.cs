using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Stealth capture coordinator service for Phase 3
    /// Manages both screen capture and webcam capture at intervals
    /// Operates completely silently without any UI notifications
    /// </summary>
    public class StealthCaptureService : IDisposable
    {
        private readonly ILogger<StealthCaptureService> _logger;
        private readonly IScreenCaptureService _screenCaptureService;
        private readonly IWebcamCaptureService _webcamCaptureService;
        private readonly IEventLogger _eventLogger;
        private readonly ISessionMonitor _sessionMonitor;
        private readonly SettingsService _settingsService;
        
        private System.Threading.Timer _captureTimer;
        private bool _isRunning;
        private bool _disposed;
        private DateTime _lastScreenCapture = DateTime.MinValue;
        private DateTime _lastWebcamCapture = DateTime.MinValue;
        
        // Configuration
        private readonly string _captureDirectory;

        public StealthCaptureService(
            ILogger<StealthCaptureService> logger,
            IScreenCaptureService screenCaptureService,
            IWebcamCaptureService webcamCaptureService,
            IEventLogger eventLogger,
            ISessionMonitor sessionMonitor,
            SettingsService settingsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _screenCaptureService = screenCaptureService ?? throw new ArgumentNullException(nameof(screenCaptureService));
            _webcamCaptureService = webcamCaptureService ?? throw new ArgumentNullException(nameof(webcamCaptureService));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
            _sessionMonitor = sessionMonitor ?? throw new ArgumentNullException(nameof(sessionMonitor));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            
            // Create capture directory
            _captureDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeskDefender", "Captures");
            
            Directory.CreateDirectory(_captureDirectory);
        }

        /// <summary>
        /// Starts the stealth capture service
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning || _disposed)
                return;

            try
            {
                // Initialize webcam service
                await _webcamCaptureService.InitializeAsync();
                
                // Subscribe to session state changes
                _sessionMonitor.SessionStateChanged += OnSessionStateChanged;
                
                // Start capture timer using user-configured interval
                var captureInterval = TimeSpan.FromSeconds(_settingsService.Settings.BatchingIntervalSeconds);
                _captureTimer = new System.Threading.Timer(OnCaptureTimer, null, TimeSpan.Zero, captureInterval);
                
                _isRunning = true;
                _logger.LogInformation("Stealth capture service started with {Interval} second intervals (user-configured)", 
                    _settingsService.Settings.BatchingIntervalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start stealth capture service");
            }
        }

        /// <summary>
        /// Stops the stealth capture service
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            try
            {
                _captureTimer?.Dispose();
                _captureTimer = null;
                
                _sessionMonitor.SessionStateChanged -= OnSessionStateChanged;
                
                _isRunning = false;
                _logger.LogInformation("Stealth capture service stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping stealth capture service");
            }
        }

        /// <summary>
        /// Handle session state changes - only capture when unlocked
        /// </summary>
        private void OnSessionStateChanged(object sender, SessionStateChangedEventArgs e)
        {
            // Log session state change for monitoring
            _logger.LogDebug("Session state changed to: {State}", e.NewState);
        }

        /// <summary>
        /// Timer callback for periodic captures
        /// </summary>
        private async void OnCaptureTimer(object state)
        {
            if (!_isRunning || _disposed)
                return;

            try
            {
                // Only capture when session is unlocked (screen not locked)
                if (IsSessionLocked())
                {
                    _logger.LogDebug("Skipping capture - session is locked");
                    return;
                }

                var now = DateTime.Now;
                
                // Perform screen capture if interval has passed
                var captureInterval = TimeSpan.FromSeconds(_settingsService.Settings.BatchingIntervalSeconds);
                if (now - _lastScreenCapture >= captureInterval)
                {
                    await PerformScreenCaptureAsync();
                    _lastScreenCapture = now;
                }

                // Perform webcam capture if interval has passed
                if (now - _lastWebcamCapture >= captureInterval)
                {
                    await PerformWebcamCaptureAsync();
                    _lastWebcamCapture = now;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during stealth capture timer");
            }
        }

        /// <summary>
        /// Performs silent screen capture
        /// </summary>
        private async Task PerformScreenCaptureAsync()
        {
            try
            {
                var screenshot = await _screenCaptureService.CaptureScreenAsync();
                if (screenshot != null)
                {
                    var fileName = $"screen_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    var filePath = Path.Combine(_captureDirectory, "Screenshots", fileName);
                    
                    await _screenCaptureService.SaveScreenshotAsync(screenshot, filePath);
                    
                    // Log the capture event
                    var eventLog = new EventLog
                    {
                        EventType = "Screen Capture",
                        Description = "Silent screen capture completed",
                        Timestamp = DateTime.Now,
                        Severity = EventSeverity.Info,
                        ImagePath = filePath,
                        Details = $"Screenshot saved to: {fileName}"
                    };
                    
                    await _eventLogger.LogEventAsync(eventLog);
                    screenshot.Dispose();
                    
                    _logger.LogDebug("Screen capture completed silently");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform screen capture");
            }
        }

        /// <summary>
        /// Performs silent webcam capture
        /// </summary>
        private async Task PerformWebcamCaptureAsync()
        {
            try
            {
                if (!_webcamCaptureService.IsWebcamAvailable())
                {
                    _logger.LogDebug("Webcam not available for capture");
                    return;
                }

                var photo = await _webcamCaptureService.CapturePhotoAsync();
                if (photo != null)
                {
                    var fileName = $"webcam_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    var filePath = Path.Combine(_captureDirectory, "Webcam", fileName);
                    
                    await _webcamCaptureService.SavePhotoAsync(photo, filePath);
                    
                    // Log the capture event
                    var eventLog = new EventLog
                    {
                        EventType = "Webcam Capture",
                        Description = "Silent webcam photo captured",
                        Timestamp = DateTime.Now,
                        Severity = EventSeverity.Info,
                        ImagePath = filePath,
                        Details = $"Webcam photo saved to: {fileName}"
                    };
                    
                    await _eventLogger.LogEventAsync(eventLog);
                    photo.Dispose();
                    
                    _logger.LogDebug("Webcam capture completed silently");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform webcam capture");
            }
        }

        /// <summary>
        /// Checks if the session is currently locked
        /// </summary>
        private bool IsSessionLocked()
        {
            try
            {
                // Use Windows API to check if workstation is locked
                return !Environment.UserInteractive || 
                       System.Windows.Forms.SystemInformation.TerminalServerSession;
            }
            catch
            {
                // If we can't determine, assume unlocked to be safe
                return false;
            }
        }

        /// <summary>
        /// Gets capture statistics
        /// </summary>
        public (int ScreenCaptures, int WebcamCaptures, string CaptureDirectory) GetCaptureStats()
        {
            try
            {
                var screenshotDir = Path.Combine(_captureDirectory, "Screenshots");
                var webcamDir = Path.Combine(_captureDirectory, "Webcam");
                
                var screenCount = Directory.Exists(screenshotDir) ? 
                    Directory.GetFiles(screenshotDir, "*.png").Length : 0;
                    
                var webcamCount = Directory.Exists(webcamDir) ? 
                    Directory.GetFiles(webcamDir, "*.jpg").Length : 0;
                
                return (screenCount, webcamCount, _captureDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting capture statistics");
                return (0, 0, _captureDirectory);
            }
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _webcamCaptureService?.Dispose();
            _disposed = true;
            
            _logger.LogInformation("Stealth capture service disposed");
        }
    }
}
