using System;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Service that coordinates between event batching and display services
    /// </summary>
    public class EventCoordinatorService : IDisposable
    {
        private readonly ILogger<EventCoordinatorService> _logger;
        private readonly EventBatchingService _batchingService;
        private readonly EventDisplayService _displayService;
        private readonly EventConfigurationService _configurationService;
        private bool _isStarted = false;

        public EventCoordinatorService(
            ILogger<EventCoordinatorService> logger,
            EventBatchingService batchingService,
            EventDisplayService displayService,
            EventConfigurationService configurationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _batchingService = batchingService ?? throw new ArgumentNullException(nameof(batchingService));
            _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        }

        /// <summary>
        /// Starts the event coordination system
        /// </summary>
        public void Start()
        {
            if (_isStarted)
            {
                _logger.LogWarning("Event coordinator is already started");
                return;
            }

            try
            {
                // Subscribe to summary events from the batching service
                _batchingService.SummaryReady += OnSummaryReady;

                // Display startup information
                _displayService.DisplayStartupInfo();
                _configurationService.DisplayCurrentConfiguration();

                _isStarted = true;
                _logger.LogInformation("Event coordinator started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start event coordinator");
                throw;
            }
        }

        /// <summary>
        /// Stops the event coordination system
        /// </summary>
        public void Stop()
        {
            if (!_isStarted)
            {
                return;
            }

            try
            {
                // Unsubscribe from events
                _batchingService.SummaryReady -= OnSummaryReady;

                _isStarted = false;
                _logger.LogInformation("Event coordinator stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping event coordinator");
            }
        }

        /// <summary>
        /// Event handler for when a summary is ready from the batching service
        /// </summary>
        private async void OnSummaryReady(object? sender, EventSummary summary)
        {
            try
            {
                await _displayService.DisplaySummary(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error displaying event summary");
            }
        }

        /// <summary>
        /// Sets the summary interval
        /// </summary>
        /// <param name="intervalSeconds">Interval in seconds (1-60)</param>
        /// <returns>True if successful</returns>
        public bool SetSummaryInterval(double intervalSeconds)
        {
            return _configurationService.SetSummaryInterval(intervalSeconds);
        }

        /// <summary>
        /// Gets the current summary interval
        /// </summary>
        public double GetCurrentInterval()
        {
            return _configurationService.CurrentIntervalSeconds;
        }

        /// <summary>
        /// Displays current configuration
        /// </summary>
        public void ShowConfiguration()
        {
            _configurationService.DisplayCurrentConfiguration();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
