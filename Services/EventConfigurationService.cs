using System;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Service for managing event monitoring configuration
    /// </summary>
    public class EventConfigurationService
    {
        private readonly ILogger<EventConfigurationService> _logger;
        private readonly EventBatchingService _batchingService;
        private readonly EventDisplayService _displayService;

        // Default configuration values
        private const double DefaultIntervalSeconds = 5.0;
        private const double MinIntervalSeconds = 1.0;
        private const double MaxIntervalSeconds = 60.0;

        private double _currentIntervalSeconds = DefaultIntervalSeconds;

        public EventConfigurationService(
            ILogger<EventConfigurationService> logger,
            EventBatchingService batchingService,
            EventDisplayService displayService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _batchingService = batchingService ?? throw new ArgumentNullException(nameof(batchingService));
            _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
        }

        /// <summary>
        /// Gets the current summary interval in seconds
        /// </summary>
        public double CurrentIntervalSeconds => _currentIntervalSeconds;

        /// <summary>
        /// Sets the event summary interval
        /// </summary>
        /// <param name="intervalSeconds">Interval in seconds (1-60)</param>
        /// <returns>True if the interval was set successfully, false otherwise</returns>
        public bool SetSummaryInterval(double intervalSeconds)
        {
            if (intervalSeconds < MinIntervalSeconds || intervalSeconds > MaxIntervalSeconds)
            {
                _logger.LogWarning("Invalid interval {Interval}s. Must be between {Min}s and {Max}s", 
                    intervalSeconds, MinIntervalSeconds, MaxIntervalSeconds);
                return false;
            }

            try
            {
                var timeSpan = TimeSpan.FromSeconds(intervalSeconds);
                _batchingService.SetSummaryInterval(timeSpan);
                _currentIntervalSeconds = intervalSeconds;
                
                _displayService.DisplayConfiguration(intervalSeconds);
                _logger.LogInformation("Summary interval updated to {Interval}s", intervalSeconds);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set summary interval to {Interval}s", intervalSeconds);
                return false;
            }
        }

        /// <summary>
        /// Resets the interval to the default value
        /// </summary>
        public void ResetToDefault()
        {
            SetSummaryInterval(DefaultIntervalSeconds);
            _logger.LogInformation("Summary interval reset to default ({Default}s)", DefaultIntervalSeconds);
        }

        /// <summary>
        /// Gets the valid interval range
        /// </summary>
        /// <returns>Tuple containing min and max interval values</returns>
        public (double Min, double Max) GetIntervalRange()
        {
            return (MinIntervalSeconds, MaxIntervalSeconds);
        }

        /// <summary>
        /// Displays current configuration to the user
        /// </summary>
        public void DisplayCurrentConfiguration()
        {
            _displayService.DisplayConfiguration(_currentIntervalSeconds);
            
            Console.WriteLine($"Valid interval range: {MinIntervalSeconds}-{MaxIntervalSeconds} seconds");
            Console.WriteLine("Use SetSummaryInterval(seconds) to change the interval.");
        }
    }
}
