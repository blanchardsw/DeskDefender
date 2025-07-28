using System;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for monitoring Windows session state changes (lock/unlock)
    /// </summary>
    public interface ISessionMonitor
    {
        /// <summary>
        /// Event fired when session state changes (lock/unlock)
        /// </summary>
        event EventHandler<SessionStateChangedEventArgs> SessionStateChanged;

        /// <summary>
        /// Gets whether the current session is locked
        /// </summary>
        bool IsSessionLocked { get; }

        /// <summary>
        /// Starts monitoring session state changes
        /// </summary>
        void StartMonitoring();

        /// <summary>
        /// Stops monitoring session state changes
        /// </summary>
        void StopMonitoring();
    }

    /// <summary>
    /// Event arguments for session state changes
    /// </summary>
    public class SessionStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The new session state
        /// </summary>
        public SessionState NewState { get; set; }

        /// <summary>
        /// Timestamp when the state change occurred
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Additional context about the state change
        /// </summary>
        public string? Context { get; set; }
    }

    /// <summary>
    /// Enumeration of possible session states
    /// </summary>
    public enum SessionState
    {
        /// <summary>
        /// Session is active and unlocked
        /// </summary>
        Unlocked,

        /// <summary>
        /// Session is locked
        /// </summary>
        Locked,

        /// <summary>
        /// Remote session connected
        /// </summary>
        RemoteConnect,

        /// <summary>
        /// Remote session disconnected
        /// </summary>
        RemoteDisconnect,

        /// <summary>
        /// User logged on
        /// </summary>
        Logon,

        /// <summary>
        /// User logged off
        /// </summary>
        Logoff
    }
}
