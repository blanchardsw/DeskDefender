using DeskDefender.Models.Events;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for AI-based behavior analysis and anomaly detection
    /// </summary>
    public interface IBehaviorAnalyzer
    {
        /// <summary>
        /// Analyzes an input pattern to determine if it's anomalous
        /// </summary>
        /// <param name="inputEvent">Input event to analyze</param>
        /// <returns>True if the behavior is considered anomalous</returns>
        bool IsAnomalous(InputEvent inputEvent);

        /// <summary>
        /// Trains the analyzer with normal behavior patterns
        /// </summary>
        /// <param name="trainingData">Array of normal input events for training</param>
        void TrainWithNormalBehavior(InputEvent[] trainingData);

        /// <summary>
        /// Gets the confidence level of the last analysis
        /// </summary>
        double LastConfidenceLevel { get; }

        /// <summary>
        /// Resets the learning model
        /// </summary>
        void ResetModel();
    }
}
