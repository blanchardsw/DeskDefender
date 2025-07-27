using System;
using System.Drawing;

namespace DeskDefender.Models.Events
{
    /// <summary>
    /// Event model for camera and motion detection activities
    /// Represents detection events from the camera monitoring system
    /// </summary>
    public class CameraEvent : EventLog
    {
        public CameraEvent()
        {
            EventType = "Camera";
            Source = "CameraService";
        }

        /// <summary>
        /// Type of camera detection that occurred
        /// </summary>
        public CameraDetectionType DetectionType { get; set; }

        /// <summary>
        /// Confidence level of the detection (0.0 to 1.0)
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Number of motion areas detected in the frame
        /// </summary>
        public int MotionAreas { get; set; }

        /// <summary>
        /// Percentage of frame that changed compared to previous frame
        /// </summary>
        public double FrameChangePercentage { get; set; }

        /// <summary>
        /// Camera device identifier that captured this event
        /// </summary>
        public string CameraId { get; set; }

        /// <summary>
        /// Resolution of the captured frame
        /// </summary>
        public Size FrameResolution { get; set; }

        /// <summary>
        /// Duration of motion detection session
        /// </summary>
        public TimeSpan MotionDuration { get; set; }

        /// <summary>
        /// Whether a face was detected in the frame
        /// </summary>
        public bool FaceDetected { get; set; }

        /// <summary>
        /// Number of faces detected in the frame
        /// </summary>
        public int FaceCount { get; set; }
    }

    /// <summary>
    /// Types of camera detection events
    /// </summary>
    public enum CameraDetectionType
    {
        /// <summary>
        /// Motion detected in camera feed
        /// </summary>
        Motion,

        /// <summary>
        /// Face detected in camera feed
        /// </summary>
        Face,

        /// <summary>
        /// Person/human silhouette detected
        /// </summary>
        Person,

        /// <summary>
        /// Significant change in lighting conditions
        /// </summary>
        LightingChange,

        /// <summary>
        /// Camera became unavailable or disconnected
        /// </summary>
        CameraUnavailable,

        /// <summary>
        /// Manual camera capture triggered
        /// </summary>
        ManualCapture
    }
}
