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
        if (_isConnected || _disposed)
            return _isConnected;

        try
        {
            _logger.LogInformation("🔌 Connecting to DeskDefender Service via Named Pipe: {PipeName}", _pipeName);

            _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            
            // Use proper async/await instead of blocking .Wait()
            await _pipeClient.ConnectAsync(5000); // 5 second timeout

            if (_pipeClient.IsConnected)
            {
                _isConnected = true;
                LastError = null;
                _logger.LogInformation("✅ Successfully connected to DeskDefender Service");
                
                // Start background listener for push events
                _ = Task.Run(() => ListenForPushEventsAsync());
                
                return true;
            }
            else
            {
                LastError = "Failed to connect to service within timeout";
                _logger.LogWarning("❌ Failed to connect to DeskDefender Service: {Error}", LastError);
                _pipeClient?.Dispose();
                _pipeClient = null;
                return false;
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _logger.LogError(ex, "❌ Error connecting to DeskDefender Service");
            _pipeClient?.Dispose();
            _pipeClient = null;
            _isConnected = false;
            return false;
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

    private async Task<T?> SendRequestAsync<T>(string command, Dictionary<string, object>? parameters = null)
    {
        if (!_isConnected || _pipeClient == null)
        {
            LastError = "Not connected to service";
            _logger.LogWarning("Attempted to send request while not connected to service");
            return default(T);
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
            _logger.LogDebug("🔄 Sending request to service: {Command}, JSON: {Json}", command, requestJson);

            // Use length-prefixed protocol for reliable messaging
            await SendMessageAsync(requestJson);

            // Read response with proper framing
            var responseJson = await ReadMessageAsync();
            
            if (string.IsNullOrEmpty(responseJson))
            {
                LastError = "No response from service";
                _logger.LogWarning("❌ No response received from service for command: {Command}", command);
                return default(T);
            }

            _logger.LogDebug("📥 Raw response JSON: {Json}", responseJson);

            var response = JsonSerializer.Deserialize<PipeResponse>(responseJson);

            if (response == null)
            {
                LastError = "Invalid response format from service";
                _logger.LogError("❌ Failed to deserialize response JSON: {Json}", responseJson);
                return default(T);
            }

            if (!response.Success)
            {
                LastError = response.Error;
                _logger.LogWarning("❌ Service request failed: {Error}", response.Error);
                return default(T);
            }

            _logger.LogDebug("✅ Received successful response from service: {Message}", response.Message);

            if (response.Data == null)
            {
                _logger.LogDebug("ℹ️ Response data is null for command: {Command}", command);
                return default(T);
            }

            // Improved deserialization to avoid double-serialization issues
            if (typeof(T) == typeof(object))
            {
                return (T?)response.Data;
            }

            // Handle JsonElement properly
            if (response.Data is JsonElement dataElement)
            {
                var result = dataElement.Deserialize<T>();
                _logger.LogDebug("✅ Successfully deserialized response data for command: {Command}", command);
                return result;
            }

            // Fallback to string-based deserialization
            var dataJson = response.Data.ToString();
            if (!string.IsNullOrEmpty(dataJson))
            {
                var result = JsonSerializer.Deserialize<T>(dataJson);
                _logger.LogDebug("✅ Successfully deserialized response data (fallback) for command: {Command}", command);
                return result;
            }

            _logger.LogWarning("⚠️ Could not deserialize response data for command: {Command}", command);
            return default(T);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _logger.LogError(ex, "❌ Error sending request to service: {Command}", command);
            
            // Connection might be broken, mark as disconnected
            _isConnected = false;
            return default(T);
        }
    }

    /// <summary>
    /// Sends a message using length-prefixed protocol for reliable Named Pipe communication
    /// </summary>
    /// <param name="message">JSON message to send</param>
    private async Task SendMessageAsync(string message)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var lengthBytes = BitConverter.GetBytes(messageBytes.Length);
        
        // Send length prefix first (4 bytes)
        await _pipeClient!.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        
        // Send message content
        await _pipeClient.WriteAsync(messageBytes, 0, messageBytes.Length);
        await _pipeClient.FlushAsync();
        
        _logger.LogDebug("💬 Sent message with length {Length}: {Message}", messageBytes.Length, message.Length > 200 ? message[..200] + "..." : message);
    }

    /// <summary>
    /// Reads a message using length-prefixed protocol for reliable Named Pipe communication
    /// </summary>
    /// <returns>Complete JSON message or null if failed</returns>
    private async Task<string?> ReadMessageAsync()
    {
        try
        {
            // Read length prefix (4 bytes)
            var lengthBuffer = new byte[4];
            var totalBytesRead = 0;
            
            while (totalBytesRead < 4)
            {
                var bytesRead = await _pipeClient!.ReadAsync(lengthBuffer, totalBytesRead, 4 - totalBytesRead);
                if (bytesRead == 0)
                {
                    _logger.LogWarning("⚠️ Connection closed while reading length prefix");
                    return null;
                }
                totalBytesRead += bytesRead;
            }
            
            var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
            _logger.LogDebug("📏 Expecting message of length: {Length}", messageLength);
            
            if (messageLength <= 0 || messageLength > 1024 * 1024) // 1MB max
            {
                _logger.LogError("❌ Invalid message length: {Length}", messageLength);
                return null;
            }
            
            // Read message content
            var messageBuffer = new byte[messageLength];
            totalBytesRead = 0;
            
            while (totalBytesRead < messageLength)
            {
                var bytesRead = await _pipeClient.ReadAsync(messageBuffer, totalBytesRead, messageLength - totalBytesRead);
                if (bytesRead == 0)
                {
                    _logger.LogWarning("⚠️ Connection closed while reading message content");
                    return null;
                }
                totalBytesRead += bytesRead;
            }
            
            var message = Encoding.UTF8.GetString(messageBuffer);
            _logger.LogDebug("📨 Received complete message: {Message}", message.Length > 200 ? message[..200] + "..." : message);
            
            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error reading message from pipe");
            return null;
        }
    }

    /// <summary>
    /// Background listener for push events from the Windows Service
    /// Continuously reads from the pipe to receive real-time input activity summaries
    /// </summary>
    private async Task ListenForPushEventsAsync()
    {
        _logger.LogInformation("🔊 Starting background listener for push events from service");
        
        try
        {
            while (_isConnected && !_disposed && _pipeClient?.IsConnected == true)
            {
                try
                {
                    // Heartbeat log to verify listener is active
                    _logger.LogDebug("💓 Background listener heartbeat - waiting for push events");
                    
                    // Try to read a push event message
                    var pushEventJson = await ReadMessageAsync();
                    
                    if (!string.IsNullOrEmpty(pushEventJson))
                    {
                        _logger.LogDebug("📢 Received push event: {Json}", pushEventJson.Length > 200 ? pushEventJson[..200] + "..." : pushEventJson);
                        
                        var response = JsonSerializer.Deserialize<PipeResponse>(pushEventJson);
                        
                        if (response?.Success == true && response.Data != null)
                        {
                            // Try to deserialize as InputActivitySummary
                            InputActivitySummary? summary = null;
                            
                            if (response.Data is JsonElement dataElement)
                            {
                                summary = dataElement.Deserialize<InputActivitySummary>();
                            }
                            else
                            {
                                var dataJson = response.Data.ToString();
                                if (!string.IsNullOrEmpty(dataJson))
                                {
                                    summary = JsonSerializer.Deserialize<InputActivitySummary>(dataJson);
                                }
                            }
                            
                            if (summary != null)
                            {
                                _logger.LogInformation("✅ Received input activity summary via push: {KeystrokeCount} keystrokes, {MouseMovementCount} movements, {MouseClickCount} clicks", 
                                    summary.KeystrokeCount, summary.MouseMovementCount, summary.MouseClickCount);
                                
                                // Fire event for push-received activity
                                InputActivityReceived?.Invoke(this, new ServiceInputActivityEventArgs
                                {
                                    Summary = summary,
                                    RetrievedAt = DateTime.Now,
                                    WasClearedFromService = false // Push events are not cleared
                                });
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ Failed to deserialize push event data as InputActivitySummary");
                            }
                        }
                        else
                        {
                            _logger.LogDebug("ℹ️ Received non-success push event or null data");
                        }
                    }
                    
                    // Small delay to prevent CPU spinning
                    await Task.Delay(100);
                }
                catch (Exception ex) when (!_disposed)
                {
                    _logger.LogWarning(ex, "⚠️ Error in push event listener loop - continuing");
                    await Task.Delay(1000); // Longer delay on error
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Push event listener terminated with error");
        }
        finally
        {
            _logger.LogInformation("🔇 Background push event listener stopped");
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
