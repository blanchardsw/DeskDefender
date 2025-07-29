using DeskDefender.Models.IPC;

namespace DeskDefender.Services.IPC;

/// <summary>
/// Interface for communicating with the Windows Service for input monitoring
/// Replaces SecureInputMonitor with service-based approach
/// </summary>
public interface IServiceInputMonitor : IDisposable
{
    /// <summary>
    /// Event fired when input activity summary is retrieved from service
    /// </summary>
    event EventHandler<ServiceInputActivityEventArgs>? InputActivityReceived;

    /// <summary>
    /// Connects to the Windows Service
    /// </summary>
    /// <returns>True if connection successful</returns>
    Task<bool> ConnectAsync();

    /// <summary>
    /// Disconnects from the Windows Service
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Gets input activity summary from service without clearing buffer
    /// </summary>
    /// <returns>Input activity summary</returns>
    Task<InputActivitySummary?> GetActivitySummaryAsync();

    /// <summary>
    /// Gets input activity summary from service and clears the service buffer
    /// </summary>
    /// <returns>Input activity summary</returns>
    Task<InputActivitySummary?> GetAndClearActivitySummaryAsync();

    /// <summary>
    /// Tests connection to the service
    /// </summary>
    /// <returns>True if service is reachable</returns>
    Task<bool> PingServiceAsync();

    /// <summary>
    /// Gets whether the client is connected to the service
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the last connection error if any
    /// </summary>
    string? LastError { get; }
}

/// <summary>
/// Event arguments for service input activity
/// </summary>
public class ServiceInputActivityEventArgs : EventArgs
{
    public InputActivitySummary Summary { get; set; } = new();
    public DateTime RetrievedAt { get; set; } = DateTime.Now;
    public bool WasClearedFromService { get; set; }
}
