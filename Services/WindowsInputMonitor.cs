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
    /// Now integrates with EventBatchingService for summarized event reporting
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

        // Structure for keyboard input data
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // Delegate for low-level hook procedures
        private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

        #endregion

        #region Private Fields

        private readonly ILogger<WindowsInputMonitor> _logger;
        private readonly EventBatchingService _batchingService;
        
        // Hook-related fields
        private LowLevelHookProc _keyboardProc;
        private LowLevelHookProc _mouseProc;
        private IntPtr _keyboardHookId = IntPtr.Zero;
        private IntPtr _mouseHookId = IntPtr.Zero;
        
        // Monitoring state management
        private bool _isMonitoring = false;
        private readonly object _lockObject = new object();
        
        // Input tracking variables
        private int _keystrokeCount = 0;
        private int _mouseClickCount = 0;
        private double _mouseMovementDistance = 0;
        private DateTime _sessionStartTime;
        private DateTime _lastInputTime;
        private DateTime _lastEventTime;
        private TimeSpan _sensitivityThreshold = TimeSpan.FromSeconds(30); // Default 30 seconds, should be configurable
        
        // Performance optimization for mouse movement
        private DateTime _lastMouseMoveTime;
        private readonly TimeSpan _mouseMoveThrottle = TimeSpan.FromMilliseconds(100); // Only process mouse moves every 100ms

        // Performance optimization - track mouse position for distance calculation
        private System.Drawing.Point _lastMousePosition;

        #endregion

        #region Constructor and Initialization

        /// <summary>
        /// Initializes a new instance of the WindowsInputMonitor
        /// Uses dependency injection to receive logger and batching service instances
        /// </summary>
        /// <param name="logger">Logger instance for diagnostic and debugging information</param>
        /// <param name="batchingService">Service for batching and summarizing events</param>
        public WindowsInputMonitor(ILogger<WindowsInputMonitor> logger, EventBatchingService batchingService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _batchingService = batchingService ?? throw new ArgumentNullException(nameof(batchingService));
            
            // Initialize hook procedures - these must be kept alive to prevent garbage collection
            _keyboardProc = KeyboardHookProc;
            _mouseProc = MouseHookProc;
            
            _logger.LogInformation("WindowsInputMonitor initialized with event batching");
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
                    _logger.LogInformation("Installing input monitoring hooks...");
                    
                    // Ensure hooks are installed on the UI thread
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Install low-level hooks for keyboard and mouse
                            _keyboardHookId = SetHook(_keyboardProc, WH_KEYBOARD_LL);
                            _logger.LogDebug("Keyboard hook installed: {HookId}", _keyboardHookId);
                            
                            _mouseHookId = SetHook(_mouseProc, WH_MOUSE_LL);
                            _logger.LogDebug("Mouse hook installed: {HookId}", _mouseHookId);
                        });
                    }
                    else
                    {
                        // Fallback if no dispatcher available
                        _keyboardHookId = SetHook(_keyboardProc, WH_KEYBOARD_LL);
                        _logger.LogDebug("Keyboard hook installed: {HookId}", _keyboardHookId);
                        
                        _mouseHookId = SetHook(_mouseProc, WH_MOUSE_LL);
                        _logger.LogDebug("Mouse hook installed: {HookId}", _mouseHookId);
                    }

                    if (_keyboardHookId == IntPtr.Zero)
                    {
                        var error = Marshal.GetLastWin32Error();
                        throw new InvalidOperationException($"Failed to install keyboard hook. Win32 Error: {error}");
                    }
                    
                    if (_mouseHookId == IntPtr.Zero)
                    {
                        var error = Marshal.GetLastWin32Error();
                        throw new InvalidOperationException($"Failed to install mouse hook. Win32 Error: {error}");
                    }

                    _isMonitoring = true;
                    _sessionStartTime = DateTime.Now;
                    _lastInputTime = DateTime.Now;
                    _lastEventTime = DateTime.Now;
                    
                    // Start the event batching service
                    _batchingService.Start();
                    
                    StatusChanged?.Invoke(this, true);
                    
                    _logger.LogInformation("Input monitoring started successfully with {SensitivityThreshold} sensitivity", _sensitivityThreshold);
                    
                    // Test hook functionality immediately
                    _logger.LogInformation("Hook installation complete. Please move mouse or press keys to test...");
                    
                    // Reset counters for new session
                    _keystrokeCount = 0;
                    _mouseClickCount = 0;
                    _mouseMovementDistance = 0;

                    _logger.LogInformation("Input monitoring and event batching started successfully");
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
                    // Stop the event batching service first
                    _batchingService.Stop();

                    // Generate final input event before stopping (legacy compatibility)
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
        /// Processes keyboard events and sends them to the batching service
        /// </summary>
        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                _logger.LogDebug("[HOOK] Keyboard hook procedure called - nCode: {nCode}, wParam: {wParam}", nCode, wParam);
                
                if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
                {
                    // Extract key information from the hook data
                    var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    var virtualKeyCode = (int)hookStruct.vkCode;
                    var timestamp = DateTime.Now;
                    
                    // Send individual key event to batching service
                    _batchingService.AddKeyboardEvent(virtualKeyCode, timestamp);
                    
                    // Update legacy counters for compatibility
                    _keystrokeCount++;
                    _lastInputTime = timestamp;
                    
                    _logger.LogDebug("[INPUT] Keyboard input detected: VK_{VirtualKeyCode:X2}", virtualKeyCode);
                    
                    // Note: Removed immediate CheckAndGenerateEvent() call to respect interval throttling
                    // The batching service will handle event generation based on configured intervals
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in keyboard hook procedure");
            }

            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// Low-level mouse hook procedure
        /// Processes mouse events and sends them to the batching service
        /// </summary>
        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                _logger.LogDebug("[HOOK] Mouse hook procedure called - nCode: {nCode}, wParam: {wParam}", nCode, wParam);
                
                if (nCode >= 0)
                {
                    // Get cursor position using Windows API
                    POINT cursorPos;
                    GetCursorPos(out cursorPos);
                    var timestamp = DateTime.Now;
                    
                    if (wParam == (IntPtr)WM_LBUTTONDOWN)
                    {
                        // Send left click event to batching service
                        _batchingService.AddMouseEvent(MouseEventType.LeftClick, cursorPos.X, cursorPos.Y, timestamp);
                        
                        // Update legacy counters
                        _mouseClickCount++;
                        _lastInputTime = timestamp;
                        
                        _logger.LogDebug("[INPUT] Left mouse click detected at ({X}, {Y})", cursorPos.X, cursorPos.Y);
                        // Note: Removed immediate CheckAndGenerateEvent() to respect interval throttling
                    }
                    else if (wParam == (IntPtr)WM_RBUTTONDOWN)
                    {
                        // Send right click event to batching service
                        _batchingService.AddMouseEvent(MouseEventType.RightClick, cursorPos.X, cursorPos.Y, timestamp);
                        
                        // Update legacy counters
                        _mouseClickCount++;
                        _lastInputTime = timestamp;
                        
                        _logger.LogDebug("[INPUT] Right mouse click detected at ({X}, {Y})", cursorPos.X, cursorPos.Y);
                        // Note: Removed immediate CheckAndGenerateEvent() to respect interval throttling
                    }
                    else if (wParam == (IntPtr)WM_MOUSEMOVE)
                    {
                        // Throttle mouse movement processing to prevent performance issues
                        if (timestamp - _lastMouseMoveTime < _mouseMoveThrottle)
                        {
                            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam); // Skip processing
                        }
                        
                        _lastMouseMoveTime = timestamp;
                        
                        // Calculate movement distance for legacy compatibility
                        var currentPosition = new Point(cursorPos.X, cursorPos.Y);
                        if (_lastMousePosition != Point.Empty)
                        {
                            var deltaX = currentPosition.X - _lastMousePosition.X;
                            var deltaY = currentPosition.Y - _lastMousePosition.Y;
                            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                            
                            // Only track significant movements (> 5 pixels)
                            if (distance > 5)
                            {
                                // Send mouse movement event to batching service
                                _batchingService.AddMouseEvent(MouseEventType.Move, cursorPos.X, cursorPos.Y, timestamp);
                                
                                // Update legacy tracking
                                _mouseMovementDistance += distance;
                                _lastInputTime = timestamp;
                                
                                _logger.LogDebug("[INPUT] Mouse movement detected: {Distance:F1}px", distance);
                                
                                // Only check for event generation occasionally, not on every mouse move
                                if (_mouseMovementDistance % 100 < distance) // Every ~100 pixels
                                {
                                    CheckAndGenerateEvent();
                                }
                            }
                        }
                        
                        _lastMousePosition = currentPosition;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in mouse hook procedure");
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
            var now = DateTime.Now;
            var timeSinceLastEvent = now - _lastEventTime;
            
            _logger.LogDebug("CheckAndGenerateEvent: timeSinceLastEvent={TimeSince}, threshold={Threshold}, keystrokes={Keys}, clicks={Clicks}", 
                timeSinceLastEvent, _sensitivityThreshold, _keystrokeCount, _mouseClickCount);
                
            if (timeSinceLastEvent >= _sensitivityThreshold)
            {
                _logger.LogInformation("Generating input event - threshold reached. Keystrokes: {Keys}, Clicks: {Clicks}, Movement: {Movement:F1}px", 
                    _keystrokeCount, _mouseClickCount, _mouseMovementDistance);
                    
                GenerateInputEvent();
                ResetCounters();
                _lastEventTime = now; // Reset the last event time for next event
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
                var duration = DateTime.Now - _sessionStartTime;
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
            _sessionStartTime = DateTime.Now;
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
