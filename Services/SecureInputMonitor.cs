using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using DeskDefender.Interfaces;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Secure input monitor that can capture keystrokes even during screen lock
    /// Uses low-level Windows API hooks for comprehensive input monitoring
    /// </summary>
    public class SecureInputMonitor : IInputMonitor, IDisposable
    {
        #region Windows API Declarations

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;

        private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

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
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        #endregion

        #region Fields

        private readonly ILogger<SecureInputMonitor> _logger;
        private readonly IEventLogger _eventLogger;
        private IntPtr _keyboardHookID = IntPtr.Zero;
        private IntPtr _mouseHookID = IntPtr.Zero;
        private LowLevelHookProc _keyboardProc;
        private LowLevelHookProc _mouseProc;
        private bool _isMonitoring;
        private bool _disposed;
        private double _sensitivityThreshold = 0.5;
        private DateTime _lastInputTime = DateTime.MinValue;
        private int _inputEventCount = 0;
        private readonly object _lockObject = new object();

        #endregion

        #region Events

        public event EventHandler<InputEvent> InputDetected;
        public event EventHandler<bool> StatusChanged;

        #endregion

        #region Constructor

        public SecureInputMonitor(ILogger<SecureInputMonitor> logger, IEventLogger eventLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));

            _keyboardProc = KeyboardHookProc;
            _mouseProc = MouseHookProc;

            _logger.LogInformation("SecureInputMonitor initialized with low-level Windows API hooks");
        }

        #endregion

        #region IInputMonitor Implementation

        public bool IsRunning => _isMonitoring;

        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SecureInputMonitor));

            if (_isMonitoring)
            {
                _logger.LogWarning("Secure input monitoring is already running");
                return;
            }

            try
            {
                _logger.LogInformation("Starting secure input monitoring with low-level hooks...");

                // Install low-level keyboard hook
                _keyboardHookID = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc,
                    GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);

                if (_keyboardHookID == IntPtr.Zero)
                {
                    var error = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"Failed to install keyboard hook. Error: {error}");
                }

                // Install low-level mouse hook
                _mouseHookID = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc,
                    GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);

                if (_mouseHookID == IntPtr.Zero)
                {
                    var error = Marshal.GetLastWin32Error();
                    UnhookWindowsHookEx(_keyboardHookID);
                    _keyboardHookID = IntPtr.Zero;
                    throw new InvalidOperationException($"Failed to install mouse hook. Error: {error}");
                }

                _isMonitoring = true;
                StatusChanged?.Invoke(this, true);

                _logger.LogInformation("Secure input monitoring started successfully with low-level hooks");
                _ = Task.Run(async () => await LogSecureInputEvent("Secure input monitoring started - can capture during screen lock", EventSeverity.Info));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start secure input monitoring");
                throw;
            }
        }

        public void Stop()
        {
            if (!_isMonitoring)
                return;

            try
            {
                _logger.LogInformation("Stopping secure input monitoring...");

                if (_keyboardHookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_keyboardHookID);
                    _keyboardHookID = IntPtr.Zero;
                }

                if (_mouseHookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHookID);
                    _mouseHookID = IntPtr.Zero;
                }

                _isMonitoring = false;
                StatusChanged?.Invoke(this, false);

                _logger.LogInformation("Secure input monitoring stopped successfully");
                _ = Task.Run(async () => await LogSecureInputEvent("Secure input monitoring stopped", EventSeverity.Info));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping secure input monitoring");
            }
        }

        public void SetSensitivity(double threshold)
        {
            _sensitivityThreshold = Math.Max(0.1, Math.Min(1.0, threshold));
            _logger.LogInformation("Secure input sensitivity set to {Threshold}", _sensitivityThreshold);
        }

        public void SetSensitivity(TimeSpan threshold)
        {
            // Convert TimeSpan to double for compatibility
            _sensitivityThreshold = Math.Max(0.1, Math.Min(1.0, threshold.TotalSeconds / 10.0));
            _logger.LogInformation("Secure input sensitivity set to {Threshold} (from TimeSpan {TimeSpan})", _sensitivityThreshold, threshold);
        }

        public TimeSpan GetIdleTime()
        {
            lock (_lockObject)
            {
                if (_lastInputTime == DateTime.MinValue)
                    return TimeSpan.Zero;
                
                return DateTime.Now - _lastInputTime;
            }
        }

        #endregion

        #region Hook Procedures

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
                {
                    var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    var keyCode = (Keys)hookStruct.vkCode;

                    lock (_lockObject)
                    {
                        _inputEventCount++;
                        _lastInputTime = DateTime.Now;

                        // Log keystroke event (be careful with sensitive data)
                        var inputEvent = new InputEvent
                        {
                            EventType = "Input",
                            Description = $"Keystroke detected: {GetSafeKeyDescription(keyCode)}",
                            Details = $"Key: {GetSafeKeyDescription(keyCode)}, Session Locked: {IsSessionLocked()}",
                            Severity = IsSessionLocked() ? EventSeverity.Warning : EventSeverity.Info,
                            Source = "SecureInputMonitor",
                            Timestamp = DateTime.Now,
                            Type = Models.Events.InputType.Keyboard,
                            Duration = TimeSpan.FromMilliseconds(100),
                            KeystrokeCount = 1
                        };

                        InputDetected?.Invoke(this, inputEvent);

                        // Log to database (fire-and-forget)
                        _ = Task.Run(async () => await LogKeystrokeEvent(keyCode, IsSessionLocked()));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in keyboard hook procedure");
            }

            return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
        }

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN || wParam == (IntPtr)WM_MBUTTONDOWN))
                {
                    var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                    lock (_lockObject)
                    {
                        _inputEventCount++;
                        _lastInputTime = DateTime.Now;

                        var buttonType = wParam == (IntPtr)WM_LBUTTONDOWN ? "Left" :
                                        wParam == (IntPtr)WM_RBUTTONDOWN ? "Right" : "Middle";

                        // Log mouse event
                        var inputEvent = new InputEvent
                        {
                            EventType = "Input",
                            Description = $"Mouse {buttonType} click detected",
                            Details = $"Button: {buttonType}, Position: ({hookStruct.pt.x}, {hookStruct.pt.y}), Session Locked: {IsSessionLocked()}",
                            Severity = IsSessionLocked() ? EventSeverity.Warning : EventSeverity.Info,
                            Source = "SecureInputMonitor",
                            Timestamp = DateTime.Now,
                            Type = Models.Events.InputType.Mouse,
                            Duration = TimeSpan.FromMilliseconds(50),
                            KeystrokeCount = 0
                        };

                        InputDetected?.Invoke(this, inputEvent);

                        // Log to database (fire-and-forget)
                        _ = Task.Run(async () => await LogMouseEvent(buttonType, hookStruct.pt.x, hookStruct.pt.y, IsSessionLocked()));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in mouse hook procedure");
            }

            return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }

        #endregion

        #region Helper Methods

        private bool IsSessionLocked()
        {
            try
            {
                // Check if the current desktop is the secure desktop (lock screen)
                var foregroundWindow = GetForegroundWindow();
                return foregroundWindow == IntPtr.Zero || IsSecureDesktop();
            }
            catch
            {
                return false;
            }
        }

        private bool IsSecureDesktop()
        {
            try
            {
                // Additional check for secure desktop
                // This is a simplified check - in practice, you might need more sophisticated detection
                var currentProcess = Process.GetCurrentProcess();
                return currentProcess.SessionId == 0; // Session 0 is typically the secure session
            }
            catch
            {
                return false;
            }
        }

        private string GetSafeKeyDescription(Keys key)
        {
            // Return safe descriptions for keys, avoiding logging sensitive information
            return key switch
            {
                Keys.Back => "Backspace",
                Keys.Tab => "Tab",
                Keys.Enter => "Enter",
                Keys.Shift or Keys.LShiftKey or Keys.RShiftKey => "Shift",
                Keys.Control or Keys.LControlKey or Keys.RControlKey => "Ctrl",
                Keys.Alt or Keys.LMenu or Keys.RMenu => "Alt",
                Keys.Space => "Space",
                Keys.Delete => "Delete",
                Keys.Escape => "Escape",
                Keys.F1 => "F1", Keys.F2 => "F2", Keys.F3 => "F3", Keys.F4 => "F4",
                Keys.F5 => "F5", Keys.F6 => "F6", Keys.F7 => "F7", Keys.F8 => "F8",
                Keys.F9 => "F9", Keys.F10 => "F10", Keys.F11 => "F11", Keys.F12 => "F12",
                _ when (key >= Keys.A && key <= Keys.Z) => "[Letter]",
                _ when (key >= Keys.D0 && key <= Keys.D9) => "[Number]",
                _ when (key >= Keys.NumPad0 && key <= Keys.NumPad9) => "[NumPad]",
                _ => "[Special Key]"
            };
        }

        private async Task LogKeystrokeEvent(Keys key, bool isLocked)
        {
            try
            {
                var eventLog = new Models.Events.EventLog
                {
                    EventType = "Input",
                    Description = $"Keystroke detected: {GetSafeKeyDescription(key)}",
                    Details = $"Key: {GetSafeKeyDescription(key)}, Session Locked: {isLocked}, Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    Severity = isLocked ? EventSeverity.Warning : EventSeverity.Info,
                    Source = "SecureInputMonitor",
                    Timestamp = DateTime.Now,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        InputType = "Keyboard",
                        KeyDescription = GetSafeKeyDescription(key),
                        SessionLocked = isLocked,
                        SecureCapture = true
                    })
                };

                await _eventLogger.LogAsync(eventLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging keystroke event");
            }
        }

        private async Task LogMouseEvent(string buttonType, int x, int y, bool isLocked)
        {
            try
            {
                var eventLog = new Models.Events.EventLog
                {
                    EventType = "Input",
                    Description = $"Mouse {buttonType} click detected",
                    Details = $"Button: {buttonType}, Position: ({x}, {y}), Session Locked: {isLocked}, Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    Severity = isLocked ? EventSeverity.Warning : EventSeverity.Info,
                    Source = "SecureInputMonitor",
                    Timestamp = DateTime.Now,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        InputType = "Mouse",
                        ButtonType = buttonType,
                        X = x,
                        Y = y,
                        SessionLocked = isLocked,
                        SecureCapture = true
                    })
                };

                await _eventLogger.LogAsync(eventLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging mouse event");
            }
        }

        private async Task LogSecureInputEvent(string message, EventSeverity severity)
        {
            try
            {
                var eventLog = new Models.Events.EventLog
                {
                    EventType = "System",
                    Description = message,
                    Details = $"Secure input monitoring event at {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    Severity = severity,
                    Source = "SecureInputMonitor",
                    Timestamp = DateTime.Now,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        ServiceEvent = true,
                        SecureMonitoring = true,
                        HooksInstalled = _keyboardHookID != IntPtr.Zero && _mouseHookID != IntPtr.Zero
                    })
                };

                await _eventLogger.LogAsync(eventLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging secure input event");
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _disposed = true;
            _logger.LogInformation("SecureInputMonitor disposed");
        }

        #endregion
    }
}
