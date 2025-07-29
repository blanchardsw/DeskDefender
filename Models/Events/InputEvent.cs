using System;
using System.Collections.Generic;

namespace DeskDefender.Models.Events
{
    /// <summary>
    /// Event model for input-related activities (keyboard/mouse)
    /// </summary>
    public class InputEvent : EventLog
    {
        public InputEvent()
        {
            EventType = "Input";
            Source = "InputMonitor";
        }

        /// <summary>
        /// Type of input detected
        /// </summary>
        public InputType Type { get; set; }

        /// <summary>
        /// Duration of the input session
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Number of keystrokes detected
        /// </summary>
        public int KeystrokeCount { get; set; }

        /// <summary>
        /// Number of mouse clicks detected
        /// </summary>
        public int MouseClickCount { get; set; }

        /// <summary>
        /// Mouse movement distance in pixels
        /// </summary>
        public double MouseMovementDistance { get; set; }

        /// <summary>
        /// Average typing speed (characters per minute)
        /// </summary>
        public double TypingSpeed { get; set; }

        /// <summary>
        /// System idle time before this input
        /// </summary>
        public TimeSpan PreviousIdleTime { get; set; }

        /// <summary>
        /// Additional structured data for activity summaries
        /// </summary>
        public Dictionary<string, object>? ActivityData { get; set; }
    }

    /// <summary>
    /// Types of input that can be detected
    /// </summary>
    public enum InputType
    {
        Keyboard,
        Mouse,
        Combined
    }
}
