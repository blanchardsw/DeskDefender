using System;
using System.Drawing;
using System.Threading.Tasks;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for camera operations and image capture
    /// </summary>
    public interface ICameraService : IMonitorService
    {
        /// <summary>
        /// Event fired when motion is detected
        /// </summary>
        event EventHandler<MotionDetectedEventArgs> MotionDetected;

        /// <summary>
        /// Event fired when a frame is captured from the camera
        /// </summary>
        event EventHandler<Bitmap> FrameCaptured;

        /// <summary>
        /// Captures a single frame from the camera
        /// </summary>
        /// <returns>Captured image as Bitmap</returns>
        Task<Bitmap> CaptureFrameAsync();

        /// <summary>
        /// Gets the current camera frame for live preview
        /// </summary>
        /// <returns>Current frame as Bitmap</returns>
        Bitmap GetCurrentFrame();

        /// <summary>
        /// Sets motion detection sensitivity
        /// </summary>
        /// <param name="sensitivity">Sensitivity level (0.0 to 1.0)</param>
        void SetMotionSensitivity(double sensitivity);

        /// <summary>
        /// Gets available camera devices
        /// </summary>
        /// <returns>Array of camera device names</returns>
        string[] GetAvailableCameras();

        /// <summary>
        /// Selects which camera to use
        /// </summary>
        /// <param name="cameraIndex">Index of the camera to use</param>
        void SelectCamera(int cameraIndex);
    }

    /// <summary>
    /// Event arguments for motion detection
    /// </summary>
    public class MotionDetectedEventArgs : EventArgs
    {
        public Bitmap CapturedFrame { get; set; }
        public double MotionLevel { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
