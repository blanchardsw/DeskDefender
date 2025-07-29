using System.Text.Json.Serialization;

namespace DeskDefender.Models.IPC;

/// <summary>
/// Summary of input activity captured during screen lock
/// Contains only metadata - no sensitive keystroke data
/// </summary>
public class InputActivitySummary
{
    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime EndTime { get; set; }

    [JsonPropertyName("keystrokeCount")]
    public int KeystrokeCount { get; set; }

    [JsonPropertyName("mouseMovementCount")]
    public int MouseMovementCount { get; set; }

    [JsonPropertyName("mouseClickCount")]
    public int MouseClickCount { get; set; }

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("capturedDuringLock")]
    public bool CapturedDuringLock { get; set; }

    [JsonPropertyName("deviceTypes")]
    public List<string> DeviceTypes { get; set; } = new();

    /// <summary>
    /// Gets the total duration of the activity period
    /// </summary>
    [JsonIgnore]
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Checks if this summary contains any activity
    /// </summary>
    [JsonIgnore]
    public bool HasActivity => KeystrokeCount > 0 || MouseMovementCount > 0 || MouseClickCount > 0;

    public override string ToString()
    {
        return $"Input Activity: {KeystrokeCount} keystrokes, {MouseMovementCount} mouse movements, {MouseClickCount} clicks over {Duration.TotalSeconds:F1}s";
    }
}

/// <summary>
/// Types of input activity that can be detected
/// </summary>
public enum InputActivityType
{
    Keystroke,
    MouseMovement,
    MouseClick,
    Unknown
}

/// <summary>
/// Event arguments for input activity detection
/// </summary>
public class InputActivityEventArgs : EventArgs
{
    public DateTime Timestamp { get; set; }
    public InputActivityType ActivityType { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public bool DuringScreenLock { get; set; }
}
