using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DeskDefender.Models.IPC;

namespace DeskDefender.Services.IPC;

/// <summary>
/// Named Pipe client for communicating with DeskDefender Windows Service
/// Provides input activity data from Session 0 service to user session app
/// </summary>
public class ServiceInputMonitor : IServiceInputMonitor
{
    private readonly ILogger<ServiceInputMonitor> _logger;
    private readonly string _pipeName = "DeskDefenderInputMonitor";
    private NamedPipeClientStream? _pipeClient;
    private bool _isConnected = false;
    private bool _disposed = false;
    private readonly object _connectionLock = new();

    public event EventHandler<ServiceInputActivityEventArgs>? InputActivityReceived;
    public bool IsConnected => _isConnected;
    public string? LastError { get; private set; }

    public ServiceInputMonitor(ILogger<ServiceInputMonitor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> ConnectAsync()
    {
        lock (_connectionLock)
        {
            if (_isConnected || _disposed)
                return _isConnected;

            try
            {
                _logger.LogInformation("Connecting to DeskDefender Service via Named Pipe: {PipeName}", _pipeName);

                _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                
                // Connect with timeout
                var connectTask = _pipeClient.ConnectAsync(5000); // 5 second timeout
                connectTask.Wait();

                if (_pipeClient.IsConnected)
                {
                    _isConnected = true;
                    LastError = null;
                    _logger.LogInformation("Successfully connected to DeskDefender Service");
                    return true;
                }
                else
                {
                    LastError = "Failed to connect to service within timeout";
                    _logger.LogWarning("Failed to connect to DeskDefender Service: {Error}", LastError);
                    _pipeClient?.Dispose();
                    _pipeClient = null;
                    return false;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                _logger.LogError(ex, "Error connecting to DeskDefender Service");
                _pipeClient?.Dispose();
                _pipeClient = null;
                _isConnected = false;
                return false;
            }
        }
    }

    public async Task DisconnectAsync()
    {
        lock (_connectionLock)
        {
            if (!_isConnected)
                return;

            try
            {
                _logger.LogInformation("Disconnecting from DeskDefender Service");

                _pipeClient?.Close();
                _pipeClient?.Dispose();
                _pipeClient = null;
                _isConnected = false;
                LastError = null;

                _logger.LogInformation("Disconnected from DeskDefender Service");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from DeskDefender Service");
                LastError = ex.Message;
            }
        }
    }

    public async Task<InputActivitySummary?> GetActivitySummaryAsync()
    {
        return await SendRequestAsync<InputActivitySummary>("getsummary");
    }

    public async Task<InputActivitySummary?> GetAndClearActivitySummaryAsync()
    {
        var summary = await SendRequestAsync<InputActivitySummary>("getandclear");
        
        if (summary != null)
        {
            // Fire event for retrieved activity
            InputActivityReceived?.Invoke(this, new ServiceInputActivityEventArgs
            {
                Summary = summary,
                RetrievedAt = DateTime.Now,
                WasClearedFromService = true
            });
        }

        return summary;
    }

    public async Task<bool> PingServiceAsync()
    {
        try
        {
            var response = await SendRequestAsync<object>("ping");
            return response != null;
        }
        catch
        {
            return false;
        }
    }

    private async Task<T?> SendRequestAsync<T>(string command, object? parameters = null) where T : class
    {
        if (!_isConnected || _pipeClient == null)
        {
            LastError = "Not connected to service";
            _logger.LogWarning("Attempted to send request while not connected to service");
            return null;
        }

        try
        {
            var request = new PipeRequest
            {
                Command = command,
                Parameters = parameters,
                ClientId = Environment.MachineName + "_" + Environment.UserName,
                Timestamp = DateTime.Now
            };

            var requestJson = JsonSerializer.Serialize(request);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);

            _logger.LogDebug("Sending request to service: {Command}", command);

            // Send request
            await _pipeClient.WriteAsync(requestBytes, 0, requestBytes.Length);
            await _pipeClient.FlushAsync();

            // Read response
            var buffer = new byte[4096];
            var bytesRead = await _pipeClient.ReadAsync(buffer, 0, buffer.Length);
            
            if (bytesRead == 0)
            {
                LastError = "No response from service";
                return null;
            }

            var responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var response = JsonSerializer.Deserialize<PipeResponse>(responseJson);

            if (response == null)
            {
                LastError = "Invalid response format from service";
                return null;
            }

            if (!response.Success)
            {
                LastError = response.Error;
                _logger.LogWarning("Service request failed: {Error}", response.Error);
                return null;
            }

            _logger.LogDebug("Received successful response from service: {Message}", response.Message);

            if (response.Data == null)
                return null;

            // Deserialize the data
            if (typeof(T) == typeof(object))
            {
                return response.Data as T;
            }

            var dataJson = JsonSerializer.Serialize(response.Data);
            return JsonSerializer.Deserialize<T>(dataJson);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _logger.LogError(ex, "Error sending request to service: {Command}", command);
            
            // Connection might be broken, mark as disconnected
            _isConnected = false;
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            DisconnectAsync().Wait(2000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing ServiceInputMonitor");
        }
        finally
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Request model for Named Pipe communication (client-side copy)
/// </summary>
public class PipeRequest
{
    public string Command { get; set; } = string.Empty;
    public object? Parameters { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// Response model for Named Pipe communication (client-side copy)
/// </summary>
public class PipeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
