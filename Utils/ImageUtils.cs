using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

namespace DeskDefender.Utils
{
    /// <summary>
    /// Utility class for image processing operations
    /// </summary>
    public static class ImageUtils
    {
        /// <summary>
        /// Saves a bitmap to the specified path with timestamp
        /// </summary>
        /// <param name="bitmap">Bitmap to save</param>
        /// <param name="directory">Directory to save the image</param>
        /// <param name="prefix">Filename prefix</param>
        /// <returns>Path to the saved image</returns>
        public static async Task<string> SaveImageWithTimestampAsync(Bitmap bitmap, string directory, string prefix = "capture")
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var filename = $"{prefix}_{timestamp}.jpg";
            var filePath = Path.Combine(directory, filename);

            await Task.Run(() =>
            {
                bitmap.Save(filePath, ImageFormat.Jpeg);
            });

            return filePath;
        }

        /// <summary>
        /// Resizes an image while maintaining aspect ratio
        /// </summary>
        /// <param name="original">Original bitmap</param>
        /// <param name="maxWidth">Maximum width</param>
        /// <param name="maxHeight">Maximum height</param>
        /// <returns>Resized bitmap</returns>
        public static Bitmap ResizeImage(Bitmap original, int maxWidth, int maxHeight)
        {
            var ratioX = (double)maxWidth / original.Width;
            var ratioY = (double)maxHeight / original.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(original.Width * ratio);
            var newHeight = (int)(original.Height * ratio);

            var resized = new Bitmap(newWidth, newHeight);
            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.DrawImage(original, 0, 0, newWidth, newHeight);
            }

            return resized;
        }

        /// <summary>
        /// Converts a bitmap to base64 string for transmission
        /// </summary>
        /// <param name="bitmap">Bitmap to convert</param>
        /// <returns>Base64 encoded string</returns>
        public static string BitmapToBase64(Bitmap bitmap)
        {
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Jpeg);
                var imageBytes = ms.ToArray();
                return Convert.ToBase64String(imageBytes);
            }
        }

        /// <summary>
        /// Converts a base64 string back to bitmap
        /// </summary>
        /// <param name="base64String">Base64 encoded image</param>
        /// <returns>Bitmap object</returns>
        public static Bitmap Base64ToBitmap(string base64String)
        {
            var imageBytes = Convert.FromBase64String(base64String);
            using (var ms = new MemoryStream(imageBytes))
            {
                return new Bitmap(ms);
            }
        }

        /// <summary>
        /// Creates a thumbnail from an image file
        /// </summary>
        /// <param name="imagePath">Path to the original image</param>
        /// <param name="thumbnailSize">Size of the thumbnail</param>
        /// <returns>Thumbnail bitmap</returns>
        public static Bitmap CreateThumbnail(string imagePath, Size thumbnailSize)
        {
            using (var original = new Bitmap(imagePath))
            {
                return ResizeImage(original, thumbnailSize.Width, thumbnailSize.Height);
            }
        }

        /// <summary>
        /// Gets the file size of an image in bytes
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>File size in bytes</returns>
        public static long GetImageFileSize(string imagePath)
        {
            if (File.Exists(imagePath))
            {
                var fileInfo = new FileInfo(imagePath);
                return fileInfo.Length;
            }
            return 0;
        }

        /// <summary>
        /// Validates if a file is a valid image
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>True if the file is a valid image</returns>
        public static bool IsValidImage(string filePath)
        {
            try
            {
                using (var img = Image.FromFile(filePath))
                {
                    return img.RawFormat.Equals(ImageFormat.Jpeg) ||
                           img.RawFormat.Equals(ImageFormat.Png) ||
                           img.RawFormat.Equals(ImageFormat.Bmp) ||
                           img.RawFormat.Equals(ImageFormat.Gif);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
