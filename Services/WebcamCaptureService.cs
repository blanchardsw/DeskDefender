using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using Microsoft.Extensions.Logging;
using AForge.Video;
using AForge.Video.DirectShow;

namespace DeskDefender.Services
{
    /// <summary>
    /// Stealth webcam capture service for Phase 3
    /// Captures photos from webcam silently without any UI notifications or alerts
    /// </summary>
    public class WebcamCaptureService : IWebcamCaptureService, IDisposable
    {
        private readonly ILogger<WebcamCaptureService> _logger;
        private VideoCaptureDevice _videoDevice;
        private Bitmap _lastFrame;
        private bool _isInitialized;
        private bool _disposed;
        private readonly object _frameLock = new object();

        public WebcamCaptureService(ILogger<WebcamCaptureService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes the webcam service silently
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized || _disposed)
                return;

            try
            {
                // Get available video devices
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                
                if (videoDevices.Count == 0)
                {
                    _logger.LogWarning("No webcam devices found");
                    return;
                }

                // Use the first available webcam
                _videoDevice = new VideoCaptureDevice(videoDevices[0].MonikerString);
                
                // Set up frame capture handler
                _videoDevice.NewFrame += OnNewFrame;
                
                // Start the video device silently
                _videoDevice.Start();
                
                // Wait a moment for the device to initialize
                await Task.Delay(1000);
                
                _isInitialized = true;
                _logger.LogInformation("Webcam service initialized silently");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize webcam service");
            }
        }

        /// <summary>
        /// Event handler for new frames from webcam
        /// </summary>
        private void OnNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                lock (_frameLock)
                {
                    // Dispose previous frame
                    _lastFrame?.Dispose();
                    
                    // Clone the new frame
                    _lastFrame = new Bitmap(eventArgs.Frame);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webcam frame");
            }
        }

        /// <summary>
        /// Captures a photo from the webcam silently
        /// </summary>
        public async Task<Bitmap> CapturePhotoAsync()
        {
            if (!_isInitialized || _disposed)
            {
                _logger.LogWarning("Webcam service not initialized");
                return null;
            }

            try
            {
                // Wait a moment to ensure we have a fresh frame
                await Task.Delay(500);

                lock (_frameLock)
                {
                    if (_lastFrame != null)
                    {
                        // Return a copy of the last frame
                        var photo = new Bitmap(_lastFrame);
                        _logger.LogDebug("Webcam photo captured silently");
                        return photo;
                    }
                }

                _logger.LogWarning("No webcam frame available for capture");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to capture webcam photo");
                return null;
            }
        }

        /// <summary>
        /// Saves a webcam photo to the specified path silently
        /// </summary>
        public async Task SavePhotoAsync(Bitmap photo, string filePath)
        {
            if (photo == null)
            {
                _logger.LogWarning("Cannot save null photo");
                return;
            }

            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save photo silently
                await Task.Run(() =>
                {
                    photo.Save(filePath, ImageFormat.Jpeg);
                });

                _logger.LogDebug("Webcam photo saved silently to: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save webcam photo to: {FilePath}", filePath);
            }
        }

        /// <summary>
        /// Checks if webcam is available for capture
        /// </summary>
        public bool IsWebcamAvailable()
        {
            try
            {
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                return videoDevices.Count > 0 && _isInitialized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking webcam availability");
                return false;
            }
        }

        /// <summary>
        /// Releases webcam resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                if (_videoDevice != null && _videoDevice.IsRunning)
                {
                    _videoDevice.SignalToStop();
                    _videoDevice.WaitForStop();
                    _videoDevice.NewFrame -= OnNewFrame;
                    _videoDevice = null;
                }

                lock (_frameLock)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = null;
                }

                _isInitialized = false;
                _disposed = true;
                
                _logger.LogInformation("Webcam service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing webcam service");
            }
        }
    }
}
