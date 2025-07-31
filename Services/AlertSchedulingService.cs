using System;
using System.Threading;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using DeskDefender.Models.Settings;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Service responsible for scheduling and timing alert generation
    /// Follows Single Responsibility Principle - only handles scheduling logic
    /// </summary>
    public class AlertSchedulingService : IAlertSchedulingService, IDisposable
    {
        private readonly ILogger<AlertSchedulingService> _logger;
        private readonly ISettingsService _settingsService;
        private System.Threading.Timer? _alertTimer;
        private DateTime _lastAlertTime;
        private bool _isRunning;

        public event EventHandler? AlertIntervalElapsed;

        public AlertSchedulingService(
            ILogger<AlertSchedulingService> logger,
            ISettingsService settingsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _lastAlertTime = DateTime.Now;
        }

        /// <summary>
        /// Starts the alert scheduling timer
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning)
            {
                _logger.LogWarning("Alert scheduling service is already running");
                return;
            }

            try
            {
                var settings = await _settingsService.GetAlertSettingsAsync();
                var intervalMs = settings.SummaryIntervalMinutes * 60 * 1000;

                _logger.LogInformation("Starting alert scheduling service with {IntervalMinutes} minute intervals", 
                    settings.SummaryIntervalMinutes);

                _alertTimer = new System.Threading.Timer(OnAlertTimerElapsed, null, intervalMs, intervalMs);
                _isRunning = true;
                _lastAlertTime = DateTime.Now;

                _logger.LogInformation("Alert scheduling service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start alert scheduling service");
                throw;
            }
        }

        /// <summary>
        /// Stops the alert scheduling timer
        /// </summary>
        public Task StopAsync()
        {
            if (!_isRunning)
            {
                _logger.LogWarning("Alert scheduling service is not running");
                return Task.CompletedTask;
            }

            try
            {
                _logger.LogInformation("Stopping alert scheduling service");

                _alertTimer?.Dispose();
                _alertTimer = null;
                _isRunning = false;

                _logger.LogInformation("Alert scheduling service stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping alert scheduling service");
                throw;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the time range for the last alert interval
        /// </summary>
        public (DateTime StartTime, DateTime EndTime) GetLastAlertInterval()
        {
            var endTime = DateTime.Now;
            var startTime = _lastAlertTime;
            return (startTime, endTime);
        }

        /// <summary>
        /// Updates the last alert time to the current time
        /// </summary>
        public void UpdateLastAlertTime()
        {
            _lastAlertTime = DateTime.Now;
        }

        /// <summary>
        /// Gets whether the scheduling service is currently running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Timer callback that fires when alert interval elapses
        /// </summary>
        private void OnAlertTimerElapsed(object? state)
        {
            try
            {
                _logger.LogDebug("Alert timer elapsed, triggering alert interval event");
                AlertIntervalElapsed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in alert timer callback");
            }
        }

        public void Dispose()
        {
            try
            {
                _alertTimer?.Dispose();
                _alertTimer = null;
                _isRunning = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing alert scheduling service");
            }
        }
    }
}
