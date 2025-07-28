using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace DeskDefender.Services
{
    /// <summary>
    /// OpenCV-based camera service implementation for motion detection and frame capture
    /// Implements the Strategy pattern for different detection algorithms
    /// Uses the Observer pattern to notify subscribers of camera events
    /// Employs background processing for real-time video analysis
    /// </summary>
    public class OpenCvCameraService : ICameraService
    {
        #region Private Fields

        private readonly ILogger<OpenCvCameraService> _logger;
        private VideoCapture _capture;
        private Mat _previousFrame;
        private Mat _currentFrame;
        private Mat _diffFrame;
        private Mat _grayFrame;
        private Mat _blurredFrame;
        
        // Monitoring state
        private bool _isCapturing = false;
        private bool _isMonitoring = false;
        private readonly object _lockObject = new object();
        
        // Background processing
        private CancellationTokenSource _cancellationTokenSource;
        private Task _captureTask;
        
        // Motion detection parameters
        private double _motionSensitivity = 0.5;
        private int _minimumMotionArea = 500;
        private int _cameraIndex = 0;
        
        // Performance tracking
        private DateTime _lastFrameTime;
        private int _frameCount = 0;
        private double _averageFps = 0;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the OpenCvCameraService
        /// </summary>
        /// <param name="logger">Logger instance for diagnostic information</param>
        public OpenCvCameraService(ILogger<OpenCvCameraService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("OpenCvCameraService initialized");
        }

        #endregion

        #region ICameraService Implementation

        /// <summary>
        /// Gets the current status of the monitoring service
        /// </summary>
        public bool IsRunning => _isMonitoring;

        /// <summary>
        /// Event fired when the service status changes
        /// </summary>
        public event EventHandler<bool> StatusChanged;

        /// <summary>
        /// Event fired when motion is detected in the camera feed
        /// Implements the Observer pattern for loose coupling
        /// </summary>
        public event EventHandler<MotionDetectedEventArgs> MotionDetected;

        /// <summary>
        /// Event fired when a frame is captured from the camera
        /// Allows subscribers to process raw camera frames
        /// </summary>
        public event EventHandler<Bitmap> FrameCaptured;

        /// <summary>
        /// Starts camera capture and monitoring
        /// Implements the Template Method pattern with comprehensive error handling
        /// </summary>
        public void StartCapture()
        {
            lock (_lockObject)
            {
                if (_isCapturing)
                {
                    _logger.LogWarning("Camera capture is already running");
                    return;
                }

                try
                {
                    // Initialize camera capture
                    _capture = new VideoCapture(_cameraIndex);
                    
                    if (!_capture.IsOpened())
                    {
                        _logger.LogWarning("Failed to open camera at index {CameraIndex}. Camera monitoring will be disabled, but other monitoring services will continue.", _cameraIndex);
                        _isCapturing = false;
                        Cleanup();
                        return; // Don't throw exception, just return gracefully
                    }

                    // Configure camera properties for optimal performance
                    _capture.Set(VideoCaptureProperties.FrameWidth, 640);
                    _capture.Set(VideoCaptureProperties.FrameHeight, 480);
                    _capture.Set(VideoCaptureProperties.Fps, 30);

                    // Initialize processing matrices
                    _currentFrame = new Mat();
                    _previousFrame = new Mat();
                    _diffFrame = new Mat();
                    _grayFrame = new Mat();
                    _blurredFrame = new Mat();

                    _isCapturing = true;
                    _lastFrameTime = DateTime.Now;
                    
                    _logger.LogInformation("Camera capture started successfully on camera {CameraIndex}", _cameraIndex);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to start camera capture. Camera monitoring will be disabled, but other monitoring services will continue.");
                    _isCapturing = false;
                    Cleanup();
                    // Don't throw exception, just log and continue
                }
            }
        }

        /// <summary>
        /// Stops camera capture and releases resources
        /// Ensures proper cleanup of OpenCV resources
        /// </summary>
        public void StopCapture()
        {
            lock (_lockObject)
            {
                if (!_isCapturing)
                {
                    return;
                }

                try
                {
                    _isCapturing = false;
                    Cleanup();
                    _logger.LogInformation("Camera capture stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while stopping camera capture");
                }
            }
        }

        /// <summary>
        /// Captures a single frame from the camera
        /// Implements the Factory pattern for bitmap creation
        /// </summary>
        /// <returns>Bitmap representation of the current camera frame</returns>
        public Bitmap CaptureFrame()
        {
            if (!_isCapturing || _capture == null)
            {
                throw new InvalidOperationException("Camera is not capturing");
            }

            try
            {
                using (var frame = new Mat())
                {
                    if (_capture.Read(frame) && !frame.Empty())
                    {
                        var bitmap = MatToBitmap(_currentFrame);
                        
                        // Fire frame captured event
                        FrameCaptured?.Invoke(this, bitmap);
                        
                        _logger.LogDebug("Frame captured successfully");
                        return bitmap;
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to capture frame from camera");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing frame");
                throw;
            }
        }

        /// <summary>
        /// Gets the current camera status
        /// </summary>
        public bool IsCapturing => _isCapturing;

        /// <summary>
        /// Sets the motion detection sensitivity
        /// Implements the Strategy pattern for configurable detection algorithms
        /// </summary>
        /// <param name="sensitivity">Sensitivity value between 0.0 and 1.0</param>
        public void SetMotionSensitivity(double sensitivity)
        {
            if (sensitivity < 0.0 || sensitivity > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(sensitivity), "Sensitivity must be between 0.0 and 1.0");
            }

            _motionSensitivity = sensitivity;
            
            // Adjust detection parameters based on sensitivity
            _minimumMotionArea = (int)(1000 * (1.0 - sensitivity));
            
            _logger.LogInformation("Motion sensitivity set to {Sensitivity}, minimum area: {MinArea}", 
                sensitivity, _minimumMotionArea);
        }

        /// <summary>
        /// Captures a single frame from the camera asynchronously (required by ICameraService interface)
        /// </summary>
        /// <returns>Captured image as Bitmap</returns>
        public async Task<Bitmap> CaptureFrameAsync()
        {
            return await Task.Run(() => CaptureFrame());
        }

        /// <summary>
        /// Gets the current camera frame for live preview (required by ICameraService interface)
        /// </summary>
        /// <returns>Current frame as Bitmap</returns>
        public Bitmap GetCurrentFrame()
        {
            return CaptureFrame();
        }

        /// <summary>
        /// Gets available camera devices (required by ICameraService interface)
        /// </summary>
        /// <returns>Array of camera device names</returns>
        public string[] GetAvailableCameras()
        {
            try
            {
                // For simplicity in Phase 1, return default camera options
                return new[] { "Default Camera (0)", "Camera 1", "Camera 2" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available cameras");
                return new[] { "Default Camera (0)" };
            }
        }

        /// <summary>
        /// Selects which camera to use (required by ICameraService interface)
        /// </summary>
        /// <param name="cameraIndex">Index of the camera to use</param>
        public void SelectCamera(int cameraIndex)
        {
            SetCameraIndex(cameraIndex);
        }

        /// <summary>
        /// Sets the camera index to use
        /// Allows switching between multiple cameras
        /// </summary>
        /// <param name="cameraIndex">Zero-based camera index</param>
        public void SetCameraIndex(int cameraIndex)
        {
            if (cameraIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cameraIndex), "Camera index must be non-negative");
            }

            _cameraIndex = cameraIndex;
            _logger.LogInformation("Camera index set to {CameraIndex}", cameraIndex);
        }

        #endregion

        #region IMonitorService Implementation

        /// <summary>
        /// Starts the camera monitoring service
        /// Initiates background motion detection processing
        /// </summary>
        public void Start()
        {
            lock (_lockObject)
            {
                if (_isMonitoring)
                {
                    _logger.LogWarning("Camera monitoring is already running");
                    return;
                }

                try
                {
                    // Start camera capture if not already running
                    if (!_isCapturing)
                    {
                        StartCapture();
                    }

                    // Start background monitoring task
                    _cancellationTokenSource = new CancellationTokenSource();
                    _captureTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token), 
                        _cancellationTokenSource.Token);

                    _isMonitoring = true;
                    StatusChanged?.Invoke(this, true);
                    _logger.LogInformation("Camera monitoring started with camera index {CameraIndex}", _cameraIndex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start camera monitoring");
                    Stop();
                    throw;
                }
            }
        }

        /// <summary>
        /// Stops the camera monitoring service
        /// Cancels background processing and cleans up resources
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
                    _isMonitoring = false;
                    StatusChanged?.Invoke(this, false);

                    // Cancel background task
                    _cancellationTokenSource?.Cancel();
                    
                    // Wait for task completion with timeout
                    if (_captureTask != null && !_captureTask.IsCompleted)
                    {
                        _captureTask.Wait(TimeSpan.FromSeconds(5));
                    }

                    _cancellationTokenSource?.Dispose();
                    _captureTask?.Dispose();

                    // Stop camera capture
                    StopCapture();

                    _logger.LogInformation("Camera monitoring stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while stopping camera monitoring");
                }
            }
        }

        /// <summary>
        /// Gets the current monitoring status
        /// </summary>
        public bool IsMonitoring => _isMonitoring;

        #endregion

        #region Motion Detection

        /// <summary>
        /// Main monitoring loop that processes camera frames for motion detection
        /// Implements background processing with cancellation support
        /// Uses computer vision algorithms for motion analysis
        /// </summary>
        private async Task MonitoringLoop(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Camera monitoring loop started");

            try
            {
                while (!cancellationToken.IsCancellationRequested && _isCapturing)
                {
                    try
                    {
                        await ProcessFrame(cancellationToken);
                        
                        // Control frame rate to prevent excessive CPU usage
                        await Task.Delay(33, cancellationToken); // ~30 FPS
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in monitoring loop");
                        await Task.Delay(1000, cancellationToken); // Wait before retrying
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Camera monitoring loop cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in camera monitoring loop");
            }

            _logger.LogInformation("Camera monitoring loop ended");
        }

        /// <summary>
        /// Processes a single frame for motion detection
        /// Implements computer vision algorithms using OpenCV
        /// </summary>
        private async Task ProcessFrame(CancellationToken cancellationToken)
        {
            if (_capture == null || !_capture.IsOpened())
            {
                return;
            }

            // Capture current frame
            if (!_capture.Read(_currentFrame) || _currentFrame.Empty())
            {
                return;
            }

            // Update performance metrics
            UpdatePerformanceMetrics();

            // Convert to grayscale for motion detection
            Cv2.CvtColor(_currentFrame, _grayFrame, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(_grayFrame, _blurredFrame, new OpenCvSharp.Size(21, 21), 0);

            // Initialize previous frame on first run
            if (_previousFrame.Empty())
            {
                _blurredFrame.CopyTo(_previousFrame);
                return;
            }

            // Calculate frame difference for motion detection
            Cv2.Absdiff(_previousFrame, _blurredFrame, _diffFrame);
            Cv2.Threshold(_diffFrame, _diffFrame, 25, 255, ThresholdTypes.Binary);

            // Dilate to fill holes and find contours
            var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(10, 10));
            Cv2.Dilate(_diffFrame, _diffFrame, kernel);

            // Find contours (motion areas)
            Cv2.FindContours(_diffFrame, out var contours, out var hierarchy, 
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            // Analyze contours for significant motion
            var motionDetected = false;
            var totalMotionArea = 0.0;
            var motionAreas = 0;

            foreach (var contour in contours)
            {
                var area = Cv2.ContourArea(contour);
                if (area > _minimumMotionArea)
                {
                    motionDetected = true;
                    totalMotionArea += area;
                    motionAreas++;
                }
            }

            // Generate motion event if significant motion detected
            if (motionDetected)
            {
                await GenerateMotionEvent(motionAreas, totalMotionArea, cancellationToken);
            }

            // Update previous frame for next comparison
            _blurredFrame.CopyTo(_previousFrame);
        }

        /// <summary>
        /// Generates and fires a motion detection event
        /// Implements the Factory pattern for event creation
        /// </summary>
        private async Task GenerateMotionEvent(int motionAreas, double totalMotionArea, CancellationToken cancellationToken)
        {
            try
            {
                // Capture frame as bitmap for event
                var frameBitmap = MatToBitmap(_currentFrame);
                
                // Calculate frame change percentage
                var frameSize = _currentFrame.Width * _currentFrame.Height;
                var changePercentage = (totalMotionArea / frameSize) * 100;

                var cameraEvent = new CameraEvent
                {
                    DetectionType = CameraDetectionType.Motion,
                    Confidence = Math.Min(changePercentage / 10.0, 1.0), // Normalize to 0-1
                    MotionAreas = motionAreas,
                    FrameChangePercentage = changePercentage,
                    CameraId = _cameraIndex.ToString(),
                    FrameResolution = new System.Drawing.Size(_currentFrame.Width, _currentFrame.Height),
                    MotionDuration = TimeSpan.FromMilliseconds(33), // Single frame duration
                    Description = $"Motion detected: {motionAreas} areas, {changePercentage:F1}% frame change",
                    Severity = DetermineMotionSeverity(changePercentage, motionAreas)
                };

                // Fire motion detected event with correct args type
                var motionArgs = new MotionDetectedEventArgs
                {
                    CapturedFrame = frameBitmap,
                    MotionLevel = changePercentage,
                    Timestamp = DateTime.Now
                };
                
                MotionDetected?.Invoke(this, motionArgs);
                FrameCaptured?.Invoke(this, frameBitmap);

                _logger.LogDebug("Motion event generated: {Areas} areas, {Change:F1}% change", 
                    motionAreas, changePercentage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating motion event");
            }
        }

        /// <summary>
        /// Determines the severity of detected motion based on analysis
        /// Implements business logic for threat assessment
        /// </summary>
        private EventSeverity DetermineMotionSeverity(double changePercentage, int motionAreas)
        {
            // High change percentage or many motion areas indicate significant activity
            if (changePercentage > 30 || motionAreas > 5)
                return EventSeverity.High;
            if (changePercentage > 15 || motionAreas > 2)
                return EventSeverity.Medium;
            
            return EventSeverity.Low;
        }

        /// <summary>
        /// Updates performance metrics for monitoring
        /// </summary>
        private void UpdatePerformanceMetrics()
        {
            _frameCount++;
            var currentTime = DateTime.Now;
            var timeDiff = (currentTime - _lastFrameTime).TotalSeconds;
            
            if (timeDiff > 0)
            {
                var currentFps = 1.0 / timeDiff;
                _averageFps = (_averageFps * 0.9) + (currentFps * 0.1); // Exponential moving average
            }
            
            _lastFrameTime = currentTime;

            // Log performance metrics periodically
            if (_frameCount % 300 == 0) // Every ~10 seconds at 30 FPS
            {
                _logger.LogDebug("Camera performance: {Fps:F1} FPS average, {Frames} frames processed", 
                    _averageFps, _frameCount);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Converts OpenCV Mat to System.Drawing.Bitmap
        /// </summary>
        /// <param name="mat">OpenCV Mat object</param>
        /// <returns>Bitmap representation</returns>
        private Bitmap MatToBitmap(Mat mat)
        {
            if (mat == null || mat.Empty())
                return null;

            try
            {
                // Convert Mat to byte array
                mat.GetArray(out byte[] data);
                
                // Create bitmap from raw data
                var bitmap = new Bitmap(mat.Width, mat.Height, PixelFormat.Format24bppRgb);
                var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.WriteOnly, bitmap.PixelFormat);
                
                System.Runtime.InteropServices.Marshal.Copy(data, 0, bitmapData.Scan0, data.Length);
                bitmap.UnlockBits(bitmapData);
                
                return bitmap;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting Mat to Bitmap");
                return null;
            }
        }

        /// <summary>
        /// Cleans up OpenCV resources
        /// Implements the Dispose pattern for deterministic resource management
        /// </summary>
        private void Cleanup()
        {
            try
            {
                _capture?.Release();
                _capture?.Dispose();
                _capture = null;

                _currentFrame?.Dispose();
                _previousFrame?.Dispose();
                _diffFrame?.Dispose();
                _grayFrame?.Dispose();
                _blurredFrame?.Dispose();

                _currentFrame = null;
                _previousFrame = null;
                _diffFrame = null;
                _grayFrame = null;
                _blurredFrame = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Ensures proper cleanup of resources
        /// Implements the Dispose pattern
        /// </summary>
        public void Dispose()
        {
            Stop();
            Cleanup();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
