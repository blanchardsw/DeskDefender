using Microsoft.Extensions.Logging;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;
using DeskDefender.Models.IPC;
using DeskDefender.Services.IPC;

namespace DeskDefender.Services.IPC;

/// <summary>
/// Service-based secure input monitor that replaces the old SecureInputMonitor
/// Uses Windows Service + IPC for reliable lock screen input monitoring
/// </summary>
public class ServiceBasedSecureInputMonitor : IInputMonitor
{
    private readonly ILogger<ServiceBasedSecureInputMonitor> _logger;
    private readonly IEventLogger _eventLogger;
    private readonly IServiceInputMonitor _serviceInputMonitor;
    private bool _isMonitoring = false;
    private bool _disposed = false;
    private System.Threading.Timer? _retrievalTimer;
    private readonly TimeSpan _retrievalInterval = TimeSpan.FromSeconds(30); // Retrieve data every 30 seconds
    private TimeSpan _sensitivity = TimeSpan.FromSeconds(1); // Default sensitivity

    // IInputMonitor events
    public event EventHandler<InputEvent>? InputDetected;
    
    // IMonitorService events  
    public event EventHandler<bool>? StatusChanged;
    
    // IMonitorService properties
    public bool IsRunning => _isMonitoring;

    public ServiceBasedSecureInputMonitor(
        ILogger<ServiceBasedSecureInputMonitor> logger,
        IEventLogger eventLogger,
        IServiceInputMonitor serviceInputMonitor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
        _serviceInputMonitor = serviceInputMonitor ?? throw new ArgumentNullException(nameof(serviceInputMonitor));

        // Subscribe to service input activity events
        _serviceInputMonitor.InputActivityReceived += OnServiceInputActivityReceived;
    }

    // IMonitorService implementation
    public void Start()
    {
        _ = Task.Run(async () => 
        {
            try
            {
                await StartAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in async Start operation");
            }
        });
    }

    public void Stop()
    {
        _ = Task.Run(async () => 
        {
            try
            {
                await StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in async Stop operation");
            }
        });
    }

    // IInputMonitor implementation
    public TimeSpan GetIdleTime()
    {
        // For service-based monitoring, we don't track idle time directly
        // Return a default value or implement if needed
        return TimeSpan.Zero;
    }

    public void SetSensitivity(TimeSpan threshold)
    {
        _sensitivity = threshold;
        _logger.LogDebug("Input sensitivity set to {Sensitivity}", threshold);
    }

    // Async methods for internal use
    public async Task<bool> StartAsync()
    {
        if (_isMonitoring || _disposed)
            return false;

        try
        {
            _logger.LogInformation("Starting Service-based Secure Input Monitor");

            // Connect to the Windows Service
            if (!await _serviceInputMonitor.ConnectAsync())
            {
                _logger.LogError("Failed to connect to DeskDefender Windows Service");
                return false;
            }

            // Test service connection
            if (!await _serviceInputMonitor.PingServiceAsync())
            {
                _logger.LogError("DeskDefender Windows Service is not responding");
                await _serviceInputMonitor.DisconnectAsync();
                return false;
            }

            // Start periodic retrieval of input activity
            _retrievalTimer = new System.Threading.Timer(RetrieveInputActivity, null, _retrievalInterval, _retrievalInterval);

            _isMonitoring = true;
            StatusChanged?.Invoke(this, true);
            _logger.LogInformation("Service-based Secure Input Monitor started successfully");

            // Log initial connection event
            await _eventLogger.LogEventAsync(new EventLog
            {
                EventType = "Service Connection",
                Description = "Connected to DeskDefender Windows Service for lock screen input monitoring",
                Severity = EventSeverity.Info,
                Timestamp = DateTime.Now,
                Source = "ServiceBasedSecureInputMonitor"
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Service-based Secure Input Monitor");
            return false;
        }
    }

    public async Task StopAsync()
    {
        if (!_isMonitoring)
            return;

        try
        {
            _logger.LogInformation("Stopping Service-based Secure Input Monitor");

            _isMonitoring = false;
            StatusChanged?.Invoke(this, false);

            // Stop retrieval timer
            _retrievalTimer?.Dispose();
            _retrievalTimer = null;

            // Get final activity summary before disconnecting
            try
            {
                var finalSummary = await _serviceInputMonitor.GetAndClearActivitySummaryAsync();
                if (finalSummary?.HasActivity == true)
                {
                    await ProcessInputActivitySummary(finalSummary, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving final activity summary");
            }

            // Disconnect from service
            await _serviceInputMonitor.DisconnectAsync();

            _logger.LogInformation("Service-based Secure Input Monitor stopped");

            // Log disconnection event
            await _eventLogger.LogEventAsync(new EventLog
            {
                EventType = "Service Disconnection",
                Description = "Disconnected from DeskDefender Windows Service",
                Severity = EventSeverity.Info,
                Timestamp = DateTime.Now,
                Source = "ServiceBasedSecureInputMonitor"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Service-based Secure Input Monitor");
        }
    }

    private async void RetrieveInputActivity(object? state)
    {
        if (!_isMonitoring || _disposed)
            return;

        try
        {
            // Get and clear activity summary from service
            var summary = await _serviceInputMonitor.GetAndClearActivitySummaryAsync();
            
            if (summary?.HasActivity == true)
            {
                await ProcessInputActivitySummary(summary, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving input activity from service");
            
            // Try to reconnect if connection was lost
            if (!_serviceInputMonitor.IsConnected)
            {
                _logger.LogInformation("Attempting to reconnect to DeskDefender Windows Service");
                await _serviceInputMonitor.ConnectAsync();
            }
        }
    }

    private void OnServiceInputActivityReceived(object? sender, ServiceInputActivityEventArgs e)
    {
        try
        {
            if (e.Summary.HasActivity)
            {
                _ = Task.Run(async () => await ProcessInputActivitySummary(e.Summary, e.WasClearedFromService));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing service input activity event");
        }
    }

    private async Task ProcessInputActivitySummary(InputActivitySummary summary, bool wasCleared)
    {
        try
        {
            _logger.LogInformation("Processing input activity summary: {Summary}", summary.ToString());

            // Create input event summary for the application
            var inputEvent = new InputEvent
            {
                EventType = "Input Activity Summary",
                Description = $"Lock screen activity: {summary.KeystrokeCount} keystrokes, {summary.MouseMovementCount} mouse movements, {summary.MouseClickCount} clicks over {summary.Duration.TotalSeconds:F1} seconds",
                Timestamp = summary.EndTime,
                Source = "ServiceBasedSecureInputMonitor",
                Severity = summary.CapturedDuringLock ? EventSeverity.Warning : EventSeverity.Info,
                ActivityData = new Dictionary<string, object>
                {
                    ["StartTime"] = summary.StartTime,
                    ["EndTime"] = summary.EndTime,
                    ["Duration"] = summary.Duration.TotalSeconds,
                    ["KeystrokeCount"] = summary.KeystrokeCount,
                    ["MouseMovementCount"] = summary.MouseMovementCount,
                    ["MouseClickCount"] = summary.MouseClickCount,
                    ["CapturedDuringLock"] = summary.CapturedDuringLock,
                    ["DeviceTypes"] = string.Join(", ", summary.DeviceTypes),
                    ["SessionId"] = summary.SessionId,
                    ["WasClearedFromService"] = wasCleared
                }
            };

            // Log the event
            await _eventLogger.LogEventAsync(inputEvent);

            // Fire input detected event for UI updates
            InputDetected?.Invoke(this, inputEvent);

            _logger.LogDebug("Successfully processed input activity summary");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing input activity summary");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            StopAsync().Wait(5000);
            _serviceInputMonitor?.Dispose();
            _retrievalTimer?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing ServiceBasedSecureInputMonitor");
        }
        finally
        {
            _disposed = true;
        }
    }
}
