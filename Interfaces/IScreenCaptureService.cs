using System.Drawing;
using System.Threading.Tasks;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for screen capture operations
    /// </summary>
    public interface IScreenCaptureService
    {
        /// <summary>
        /// Captures the entire screen
        /// </summary>
        /// <returns>Screenshot as Bitmap</returns>
        Bitmap CaptureScreen();

        /// <summary>
        /// Captures a specific region of the screen
        /// </summary>
        /// <param name="x">X coordinate of the region</param>
        /// <param name="y">Y coordinate of the region</param>
        /// <param name="width">Width of the region</param>
        /// <param name="height">Height of the region</param>
        /// <returns>Screenshot of the specified region</returns>
        Bitmap CaptureRegion(int x, int y, int width, int height);

        /// <summary>
        /// Captures the screen asynchronously
        /// </summary>
        /// <returns>Screenshot as Bitmap</returns>
        Task<Bitmap> CaptureScreenAsync();

        /// <summary>
        /// Saves a screenshot to the specified path
        /// </summary>
        /// <param name="screenshot">Bitmap to save</param>
        /// <param name="filePath">Path where to save the image</param>
        Task SaveScreenshotAsync(Bitmap screenshot, string filePath);
    }
}
