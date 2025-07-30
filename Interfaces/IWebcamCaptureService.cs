using System;
using System.Drawing;
using System.Threading.Tasks;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for stealth webcam capture operations
    /// </summary>
    public interface IWebcamCaptureService
    {
        /// <summary>
        /// Captures a photo from the webcam silently
        /// </summary>
        /// <returns>Photo as Bitmap, or null if capture fails</returns>
        Task<Bitmap> CapturePhotoAsync();

        /// <summary>
        /// Saves a webcam photo to the specified path
        /// </summary>
        /// <param name="photo">Bitmap to save</param>
        /// <param name="filePath">Path where to save the image</param>
        Task SavePhotoAsync(Bitmap photo, string filePath);

        /// <summary>
        /// Checks if webcam is available for capture
        /// </summary>
        /// <returns>True if webcam is available</returns>
        bool IsWebcamAvailable();

        /// <summary>
        /// Initializes the webcam service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Releases webcam resources
        /// </summary>
        void Dispose();
    }
}
