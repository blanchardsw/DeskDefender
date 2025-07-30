using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using DeskDefender.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Stealth screen capture service for Phase 3
    /// Captures screenshots silently without any UI notifications or alerts
    /// </summary>
    public class ScreenCaptureService : IScreenCaptureService
    {
        private readonly ILogger<ScreenCaptureService> _logger;

        public ScreenCaptureService(ILogger<ScreenCaptureService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Captures the entire screen silently
        /// </summary>
        public Bitmap CaptureScreen()
        {
            try
            {
                // Get the size of the primary screen
                var bounds = Screen.PrimaryScreen.Bounds;
                return CaptureRegion(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to capture screen");
                return null;
            }
        }

        /// <summary>
        /// Captures a specific region of the screen silently
        /// </summary>
        public Bitmap CaptureRegion(int x, int y, int width, int height)
        {
            try
            {
                var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    // Copy from screen to bitmap - completely silent operation
                    graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                }

                _logger.LogDebug("Screen region captured silently: {Width}x{Height} at ({X},{Y})", 
                    width, height, x, y);
                return bitmap;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to capture screen region at ({X},{Y}) size {Width}x{Height}", 
                    x, y, width, height);
                return null;
            }
        }

        /// <summary>
        /// Captures the screen asynchronously and silently
        /// </summary>
        public async Task<Bitmap> CaptureScreenAsync()
        {
            return await Task.Run(() => CaptureScreen());
        }

        /// <summary>
        /// Saves a screenshot to the specified path silently
        /// </summary>
        public async Task SaveScreenshotAsync(Bitmap screenshot, string filePath)
        {
            if (screenshot == null)
            {
                _logger.LogWarning("Cannot save null screenshot");
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

                // Save screenshot silently
                await Task.Run(() =>
                {
                    screenshot.Save(filePath, ImageFormat.Png);
                });

                _logger.LogDebug("Screenshot saved silently to: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save screenshot to: {FilePath}", filePath);
            }
        }

        /// <summary>
        /// Captures all screens in a multi-monitor setup silently
        /// </summary>
        public Bitmap CaptureAllScreens()
        {
            try
            {
                // Calculate the total bounds of all screens
                var totalBounds = Rectangle.Empty;
                foreach (var screen in Screen.AllScreens)
                {
                    totalBounds = Rectangle.Union(totalBounds, screen.Bounds);
                }

                // Capture the entire desktop area
                return CaptureRegion(totalBounds.X, totalBounds.Y, totalBounds.Width, totalBounds.Height);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to capture all screens");
                return null;
            }
        }
    }
}
