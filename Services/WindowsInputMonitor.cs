using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Windows API structure for cursor position
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// Windows-specific implementation of input monitoring using low-level hooks
    /// Implements the Observer pattern to notify subscribers of input events
    /// Uses Windows API hooks to capture system-wide keyboard and mouse activity
    /// </summary>
    public class WindowsInputMonitor : IInputMonitor
    {
        #region Windows API Declarations
        
        // Windows API constants for hook types
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MOUSEMOVE = 0x0200;

        // Windows API function declarations
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        // Structure for getting last input information
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        // Delegate for low-level hook procedures
        private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

        #endregion

        #region Private Fields

        private readonly ILogger<WindowsInputMonitor> _logger;
        private IntPtr _keyboardHookId = IntPtr.Zero;
        private IntPtr _mouseHookId = IntPtr.Zero;
        private LowLevelHookProc _keyboardProc;
        private LowLevelHookProc _mouseProc;
        
        // Monitoring state management
        private bool _isMonitoring = false;
        private readonly object _lockObject = new object();
        
        // Input tracking variables
        private int _keystrokeCount = 0;
        private int _mouseClickCount = 0;
        private double _mouseMovementDistance = 0;
        private DateTime _sessionStartTime;
        private DateTime _lastInputTime;
        private TimeSpan _sensitivityThreshold = TimeSpan.FromSeconds(30);

        // Performance optimization - track mouse position for distance calculation
        private System.Drawing.Point _lastMousePosition;

        #endregion

        #region Constructor and Initialization

        /// <summary>
        /// Initializes a new instance of the WindowsInputMonitor
        /// Uses dependency injection to receive logger instance
        /// </summary>
        /// <param name="logger">Logger instance for diagnostic and debugging information</param>
        public WindowsInputMonitor(ILogger<WindowsInputMonitor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize hook procedures - these must be kept alive to prevent garbage collection
            _keyboardProc = KeyboardHookProc;
            _mouseProc = MouseHookProc;
            
            _logger.LogInformation("WindowsInputMonitor initialized");
        }

        #endregion

        #region IInputMonitor Implementation

        /// <summary>
        /// Event fired when input activity is detected
        /// Follows the Observer pattern for loose coupling between components
        /// </summary>
        public event EventHandler<InputEvent> InputDetected;

        /// <summary>
        /// Gets the current system idle time using Windows API
        /// </summary>
        /// <returns>TimeSpan representing how long the system has been idle</returns>
        public TimeSpan GetIdleTime()
        {
            var lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            
            if (GetLastInputInfo(ref lastInputInfo))
            {
                var idleTime = Environment.TickCount - lastInputInfo.dwTime;
                return TimeSpan.FromMilliseconds(idleTime);
            }
            
            return TimeSpan.Zero;
        }

        /// <summary>
        /// Sets the sensitivity threshold for input detection
        /// Implements the Strategy pattern by allowing runtime configuration
        /// </summary>
        /// <param name="threshold">Minimum time between inputs to trigger an event</param>
        public void SetSensitivity(TimeSpan threshold)
        {
            _sensitivityThreshold = threshold;
            _logger.LogInformation("Input sensitivity threshold set to {Threshold}", threshold);
        }

        #endregion

        #region IMonitorService Implementation

        /// <summary>
        /// Gets the current status of the monitoring service
        /// </summary>
        public bool IsRunning => _isMonitoring;

        /// <summary>
        /// Event fired when the service status changes
        /// </summary>
        public event EventHandler<bool> StatusChanged;

        /// <summary>
        /// Starts the input monitoring service
        /// Implements the Template Method pattern with error handling and logging
        /// </summary>
        public void Start()
        {
            lock (_lockObject)
            {
                if (_isMonitoring)
                {
                    _logger.LogWarning("Input monitoring is already running");
                    return;
                }

                try
                {
                    // Install low-level hooks for keyboard and mouse
                    _keyboardHookId = SetHook(_keyboardProc, WH_KEYBOARD_LL);
                    _mouseHookId = SetHook(_mouseProc, WH_MOUSE_LL);

                    if (_keyboardHookId == IntPtr.Zero || _mouseHookId == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Failed to install input hooks");
                    }

                    _isMonitoring = true;
                    _sessionStartTime = DateTime.UtcNow;
                    _lastInputTime = DateTime.UtcNow;
                    StatusChanged?.Invoke(this, true);
                    
                    // Reset counters for new session
                    _keystrokeCount = 0;
                    _mouseClickCount = 0;
                    _mouseMovementDistance = 0;

                    _logger.LogInformation("Input monitoring started successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start input monitoring");
                    Stop(); // Cleanup on failure
                    throw;
                }
            }
        }

        /// <summary>
        /// Stops the input monitoring service
        /// Ensures proper cleanup of system resources
        /// </summary>
        public void Stop()
        {
            lock (_lockObject)
            {
                if (!_isMonitoring)
                {
                    return;
                }

                try
                {
                    // Generate final input event before stopping
                    if (_keystrokeCount > 0 || _mouseClickCount > 0)
                    {
                        GenerateInputEvent();
                    }

                    // Unhook and cleanup
                    if (_keyboardHookId != IntPtr.Zero)
                    {
                        UnhookWindowsHookEx(_keyboardHookId);
                        _keyboardHookId = IntPtr.Zero;
                    }

                    if (_mouseHookId != IntPtr.Zero)
                    {
                        UnhookWindowsHookEx(_mouseHookId);
                        _mouseHookId = IntPtr.Zero;
                    }

                    _isMonitoring = false;
                    StatusChanged?.Invoke(this, false);
                    _logger.LogInformation("Input monitoring stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while stopping input monitoring");
                }
            }
        }

        /// <summary>
        /// Gets the current monitoring status
        /// </summary>
        public bool IsMonitoring => _isMonitoring;

        #endregion

        #region Hook Procedures

        /// <summary>
        /// Low-level keyboard hook procedure
        /// Processes keyboard events and updates tracking counters
        /// </summary>
        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                _keystrokeCount++;
                _lastInputTime = DateTime.UtcNow;
                
                // Check if we should generate an event based on sensitivity threshold
                CheckAndGenerateEvent();
            }

            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// Low-level mouse hook procedure
        /// Processes mouse events and calculates movement distance
        /// </summary>
        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    _mouseClickCount++;
                    _lastInputTime = DateTime.UtcNow;
                    // Get cursor position using Windows API
                    POINT cursorPos;
                    GetCursorPos(out cursorPos);
                    
                    // Calculate mouse movement distance for behavior analysis
                    var currentPosition = new Point(cursorPos.X, cursorPos.Y);
                    if (_lastMousePosition != Point.Empty)
                    {
                        var deltaX = currentPosition.X - _lastMousePosition.X;
                        var deltaY = currentPosition.Y - _lastMousePosition.Y;
                        _mouseMovementDistance += Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    }
                    
                    _lastMousePosition = currentPosition;
                    _lastInputTime = DateTime.UtcNow;
                    CheckAndGenerateEvent();
                }
            }

            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Installs a low-level hook using Windows API
        /// </summary>
        private IntPtr SetHook(LowLevelHookProc proc, int hookType)
        {
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(hookType, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        /// <summary>
        /// Checks if enough time has passed to generate an input event
        /// Implements debouncing to prevent excessive event generation
        /// </summary>
        private void CheckAndGenerateEvent()
        {
            var timeSinceLastEvent = DateTime.UtcNow - _sessionStartTime;
            if (timeSinceLastEvent >= _sensitivityThreshold)
            {
                GenerateInputEvent();
                ResetCounters();
            }
        }

        /// <summary>
        /// Generates and fires an InputEvent with current statistics
        /// Implements the Factory pattern for event creation
        /// </summary>
        private void GenerateInputEvent()
        {
            try
            {
                var duration = DateTime.UtcNow - _sessionStartTime;
                var idleTime = GetIdleTime();
                
                // Calculate typing speed (characters per minute)
                var typingSpeed = duration.TotalMinutes > 0 ? 
                    (_keystrokeCount / duration.TotalMinutes) : 0;

                var inputEvent = new InputEvent
                {
                    Type = DetermineInputType(),
                    Duration = duration,
                    KeystrokeCount = _keystrokeCount,
                    MouseClickCount = _mouseClickCount,
                    MouseMovementDistance = _mouseMovementDistance,
                    TypingSpeed = typingSpeed,
                    PreviousIdleTime = idleTime,
                    Description = $"Input detected: {_keystrokeCount} keystrokes, {_mouseClickCount} clicks, {_mouseMovementDistance:F1}px movement",
                    Severity = DetermineSeverity(idleTime)
                };

                // Fire event to subscribers (Observer pattern)
                InputDetected?.Invoke(this, inputEvent);
                
                _logger.LogDebug("Input event generated: {Event}", inputEvent.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating input event");
            }
        }

        /// <summary>
        /// Determines the type of input based on activity patterns
        /// </summary>
        private InputType DetermineInputType()
        {
            if (_keystrokeCount > 0 && _mouseClickCount > 0)
                return InputType.Combined;
            if (_keystrokeCount > 0)
                return InputType.Keyboard;
            return InputType.Mouse;
        }

        /// <summary>
        /// Determines event severity based on context and idle time
        /// Implements business logic for threat assessment
        /// </summary>
        private EventSeverity DetermineSeverity(TimeSpan idleTime)
        {
            // If system was idle for a long time, input is more suspicious
            if (idleTime > TimeSpan.FromHours(4))
                return EventSeverity.High;
            if (idleTime > TimeSpan.FromHours(1))
                return EventSeverity.Medium;
            
            return EventSeverity.Low;
        }

        /// <summary>
        /// Resets tracking counters for next session
        /// </summary>
        private void ResetCounters()
        {
            _keystrokeCount = 0;
            _mouseClickCount = 0;
            _mouseMovementDistance = 0;
            _sessionStartTime = DateTime.UtcNow;
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Ensures proper cleanup of system resources
        /// Implements the Dispose pattern for deterministic resource management
        /// </summary>
        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
