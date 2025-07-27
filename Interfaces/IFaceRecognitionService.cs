using System.Drawing;
using System.Threading.Tasks;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for facial recognition operations
    /// </summary>
    public interface IFaceRecognitionService
    {
        /// <summary>
        /// Determines if the face in the image is a known user
        /// </summary>
        /// <param name="image">Image containing a face to analyze</param>
        /// <returns>True if the face belongs to a known user</returns>
        Task<bool> IsKnownFaceAsync(Bitmap image);

        /// <summary>
        /// Trains the recognition system with images of known users
        /// </summary>
        /// <param name="userName">Name of the user</param>
        /// <param name="trainingImages">Array of training images for the user</param>
        Task TrainUserAsync(string userName, Bitmap[] trainingImages);

        /// <summary>
        /// Gets the confidence level of the last recognition attempt
        /// </summary>
        double LastConfidenceLevel { get; }
    }
}
