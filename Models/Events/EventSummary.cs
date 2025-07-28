using System;
using System.Collections.Generic;

namespace DeskDefender.Models.Events
{
    /// <summary>
    /// Represents a summary of events collected over a time interval
    /// </summary>
    public class EventSummary
    {
        /// <summary>
        /// Unique identifier for this summary
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Start time of the summary interval
        /// </summary>
        public DateTime IntervalStart { get; set; }

        /// <summary>
        /// End time of the summary interval
        /// </summary>
        public DateTime IntervalEnd { get; set; }

        /// <summary>
        /// Duration of the interval
        /// </summary>
        public TimeSpan IntervalDuration => IntervalEnd - IntervalStart;

        /// <summary>
        /// Keyboard activity summary
        /// </summary>
        public KeyboardSummary? KeyboardActivity { get; set; }

        /// <summary>
        /// Mouse activity summary
        /// </summary>
        public MouseSummary? MouseActivity { get; set; }

        /// <summary>
        /// Total number of raw events processed in this interval
        /// </summary>
        public int TotalEventsProcessed { get; set; }

        /// <summary>
        /// Human-readable description of the activity
        /// </summary>
        public string GetSummaryDescription()
        {
            var parts = new List<string>();

            if (KeyboardActivity != null && KeyboardActivity.HasActivity)
            {
                if (!string.IsNullOrEmpty(KeyboardActivity.TextTyped))
                {
                    parts.Add($"Typed: \"{KeyboardActivity.TextTyped}\" ({KeyboardActivity.KeystrokeCount} keystrokes)");
                }
                else
                {
                    parts.Add($"Keyboard activity: {KeyboardActivity.KeystrokeCount} keystrokes");
                }
            }

            if (MouseActivity != null && MouseActivity.HasActivity)
            {
                var mouseParts = new List<string>();
                
                if (MouseActivity.ClickCount > 0)
                {
                    mouseParts.Add($"{MouseActivity.ClickCount} clicks");
                }
                
                if (MouseActivity.MovementEvents > 0)
                {
                    mouseParts.Add($"moved {MouseActivity.TotalDistance:F0}px in {MouseActivity.MovementEvents} movements");
                }

                if (mouseParts.Count > 0)
                {
                    parts.Add($"Mouse: {string.Join(", ", mouseParts)}");
                }
            }

            if (parts.Count == 0)
            {
                return $"No significant activity during {IntervalDuration.TotalSeconds:F1}s interval";
            }

            return $"[{IntervalDuration.TotalSeconds:F1}s] {string.Join(" | ", parts)}";
        }
    }

    /// <summary>
    /// Summary of keyboard activity during an interval
    /// </summary>
    public class KeyboardSummary
    {
        /// <summary>
        /// Total number of keystrokes
        /// </summary>
        public int KeystrokeCount { get; set; }

        /// <summary>
        /// Reconstructed text that was typed (best effort)
        /// </summary>
        public string TextTyped { get; set; } = string.Empty;

        /// <summary>
        /// Whether there was any keyboard activity
        /// </summary>
        public bool HasActivity => KeystrokeCount > 0;

        /// <summary>
        /// Average typing speed during the interval (characters per minute)
        /// </summary>
        public double TypingSpeed { get; set; }
    }

    /// <summary>
    /// Summary of mouse activity during an interval
    /// </summary>
    public class MouseSummary
    {
        /// <summary>
        /// Total number of mouse clicks
        /// </summary>
        public int ClickCount { get; set; }

        /// <summary>
        /// Number of mouse movement events
        /// </summary>
        public int MovementEvents { get; set; }

        /// <summary>
        /// Total distance moved in pixels
        /// </summary>
        public double TotalDistance { get; set; }

        /// <summary>
        /// Time spent actively moving the mouse
        /// </summary>
        public TimeSpan ActiveMovementTime { get; set; }

        /// <summary>
        /// Whether there was any mouse activity
        /// </summary>
        public bool HasActivity => ClickCount > 0 || MovementEvents > 0;
    }
}
