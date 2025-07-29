using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services.Coordinators
{
    /// <summary>
    /// Coordinates the startup, shutdown, and state management of multiple monitoring services
    /// Extracted from CompositeMonitoringService for better separation of concerns and modularity
    /// </summary>
    public class MonitoringCoordinator
    {
        private readonly ILogger<MonitoringCoordinator> _logger;
        private readonly List<IMonitorService> _monitoringServices;
        private readonly object _lockObject = new object();
        private bool _isCoordinating = false;

        public MonitoringCoordinator(ILogger<MonitoringCoordinator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _monitoringServices = new List<IMonitorService>();
        }

        /// <summary>
        /// Registers a monitoring service to be coordinated
        /// </summary>
        public void RegisterService(IMonitorService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            
            lock (_lockObject)
            {
                if (!_monitoringServices.Contains(service))
                {
                    _monitoringServices.Add(service);
                    _logger.LogDebug("Registered monitoring service: {ServiceType}", service.GetType().Name);
                }
            }
        }

        /// <summary>
        /// Starts all registered monitoring services in a coordinated manner
        /// </summary>
        public async Task StartAllServicesAsync()
        {
            lock (_lockObject)
            {
                if (_isCoordinating)
                {
                    _logger.LogWarning("Monitoring services are already being coordinated");
                    return;
                }
                _isCoordinating = true;
            }

            try
            {
                _logger.LogInformation("Starting coordinated monitoring of {ServiceCount} services", _monitoringServices.Count);

                var startTasks = new List<Task>();
                
                foreach (var service in _monitoringServices)
                {
                    startTasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            service.Start();
                            _logger.LogDebug("Successfully started service: {ServiceType}", service.GetType().Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to start service: {ServiceType}", service.GetType().Name);
                        }
                    }));
                }

                await Task.WhenAll(startTasks);
                _logger.LogInformation("Coordinated startup completed for all monitoring services");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during coordinated service startup");
                throw;
            }
        }

        /// <summary>
        /// Stops all registered monitoring services in a coordinated manner
        /// </summary>
        public async Task StopAllServicesAsync()
        {
            try
            {
                _logger.LogInformation("Stopping coordinated monitoring of {ServiceCount} services", _monitoringServices.Count);

                var stopTasks = new List<Task>();
                
                foreach (var service in _monitoringServices)
                {
                    stopTasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            service.Stop();
                            _logger.LogDebug("Successfully stopped service: {ServiceType}", service.GetType().Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to stop service: {ServiceType}", service.GetType().Name);
                        }
                    }));
                }

                await Task.WhenAll(stopTasks);
                _logger.LogInformation("Coordinated shutdown completed for all monitoring services");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during coordinated service shutdown");
            }
            finally
            {
                lock (_lockObject)
                {
                    _isCoordinating = false;
                }
            }
        }

        /// <summary>
        /// Gets the current status of all registered monitoring services
        /// </summary>
        public MonitoringStatus GetOverallStatus()
        {
            lock (_lockObject)
            {
                var runningServices = _monitoringServices.Count(s => s.IsRunning);
                var totalServices = _monitoringServices.Count;
                
                return new MonitoringStatus
                {
                    IsCoordinating = _isCoordinating,
                    TotalServices = totalServices,
                    RunningServices = runningServices,
                    AllServicesRunning = runningServices == totalServices && totalServices > 0,
                    ServiceStatuses = _monitoringServices.ToDictionary(
                        s => s.GetType().Name,
                        s => s.IsRunning)
                };
            }
        }

        /// <summary>
        /// Checks if all critical services are running
        /// </summary>
        public bool AreAllCriticalServicesRunning()
        {
            lock (_lockObject)
            {
                // Define critical services that must be running for proper operation
                var criticalServiceTypes = new[]
                {
                    typeof(IInputMonitor),
                    typeof(ISessionMonitor),
                    typeof(ILoginMonitor)
                };

                foreach (var criticalType in criticalServiceTypes)
                {
                    var criticalService = _monitoringServices.FirstOrDefault(s => criticalType.IsAssignableFrom(s.GetType()));
                    if (criticalService == null || !criticalService.IsRunning)
                    {
                        _logger.LogWarning("Critical service not running: {ServiceType}", criticalType.Name);
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Restarts a specific service if it has failed
        /// </summary>
        public async Task RestartServiceAsync(Type serviceType)
        {
            var service = _monitoringServices.FirstOrDefault(s => s.GetType() == serviceType);
            if (service == null)
            {
                _logger.LogWarning("Service not found for restart: {ServiceType}", serviceType.Name);
                return;
            }

            try
            {
                _logger.LogInformation("Restarting service: {ServiceType}", serviceType.Name);
                
                await Task.Run(() =>
                {
                    service.Stop();
                    Task.Delay(1000).Wait(); // Brief pause before restart
                    service.Start();
                });
                
                _logger.LogInformation("Successfully restarted service: {ServiceType}", serviceType.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restart service: {ServiceType}", serviceType.Name);
            }
        }
    }

    /// <summary>
    /// Represents the overall status of coordinated monitoring services
    /// </summary>
    public class MonitoringStatus
    {
        public bool IsCoordinating { get; set; }
        public int TotalServices { get; set; }
        public int RunningServices { get; set; }
        public bool AllServicesRunning { get; set; }
        public Dictionary<string, bool> ServiceStatuses { get; set; } = new Dictionary<string, bool>();
    }
}
