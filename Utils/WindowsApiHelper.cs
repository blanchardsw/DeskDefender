using System;
using System.Runtime.InteropServices;

namespace DeskDefender.Utils
{
    /// <summary>
    /// Helper class for Windows API P/Invoke declarations
    /// </summary>
    public static class WindowsApiHelper
    {
        #region User32.dll imports

        /// <summary>
        /// Gets the time of the last input event
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        /// <summary>
        /// Gets the current tick count
        /// </summary>
        [DllImport("kernel32.dll")]
        public static extern uint GetTickCount();

        /// <summary>
        /// Sets a Windows hook procedure
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        /// <summary>
        /// Removes a Windows hook procedure
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        /// <summary>
        /// Calls the next hook procedure in the hook chain
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Gets a module handle
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        #endregion

        #region Structures

        /// <summary>
        /// Structure for last input information
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        /// <summary>
        /// Structure for low-level keyboard input
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// Structure for low-level mouse input
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// Point structure
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        #endregion

        #region Delegates

        /// <summary>
        /// Low-level hook procedure delegate
        /// </summary>
        public delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        #endregion

        #region Constants

        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int WM_MOUSEMOVE = 0x0200;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the system idle time
        /// </summary>
        /// <returns>Idle time as TimeSpan</returns>
        public static TimeSpan GetIdleTime()
        {
            var lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            
            if (GetLastInputInfo(ref lastInputInfo))
            {
                var idleTime = GetTickCount() - lastInputInfo.dwTime;
                return TimeSpan.FromMilliseconds(idleTime);
            }
            
            return TimeSpan.Zero;
        }

        #endregion
    }
}
