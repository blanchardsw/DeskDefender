using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeskDefender.Models.Events;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Service that batches individual input events and creates summaries at configurable intervals
    /// </summary>
    public class EventBatchingService : IDisposable
    {
        private readonly ILogger<EventBatchingService> _logger;
        private readonly System.Threading.Timer _summaryTimer;
        private readonly object _lockObject = new object();
        
        // Configuration
        private TimeSpan _summaryInterval = TimeSpan.FromSeconds(30); // Default 30 seconds
        
        // Current batch data
        private DateTime _currentBatchStart;
        private readonly List<int> _keystrokeBuffer = new List<int>(); // Store virtual key codes as integers
        private readonly List<DateTime> _keystrokeTimes = new List<DateTime>();
        private readonly List<MouseEventData> _mouseEvents = new List<MouseEventData>();
        private int _totalEventsProcessed = 0;

        /// <summary>
        /// Event fired when a summary is ready
        /// </summary>
        public event EventHandler<EventSummary>? SummaryReady;

        public EventBatchingService(ILogger<EventBatchingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentBatchStart = DateTime.UtcNow;
            
            // Create timer but don't start it yet
            _summaryTimer = new System.Threading.Timer(GenerateSummary, null, Timeout.Infinite, Timeout.Infinite);
            
            _logger.LogInformation("EventBatchingService initialized with {Interval}s interval", _summaryInterval.TotalSeconds);
        }

        /// <summary>
        /// Sets the summary interval (1-60 seconds)
        /// </summary>
        public void SetSummaryInterval(TimeSpan interval)
        {
            if (interval.TotalSeconds < 1 || interval.TotalSeconds > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be between 1 and 60 seconds");
            }

            lock (_lockObject)
            {
                _summaryInterval = interval;
                
                // Restart timer with new interval
                if (_summaryTimer != null)
                {
                    _summaryTimer.Change(_summaryInterval, _summaryInterval);
                }
                
                _logger.LogInformation("Summary interval changed to {Interval}s", interval.TotalSeconds);
            }
        }

        /// <summary>
        /// Starts the batching service
        /// </summary>
        public void Start()
        {
            lock (_lockObject)
            {
                _currentBatchStart = DateTime.UtcNow;
                _summaryTimer.Change(_summaryInterval, _summaryInterval);
                _logger.LogInformation("EventBatchingService started");
            }
        }

        /// <summary>
        /// Stops the batching service
        /// </summary>
        public void Stop()
        {
            lock (_lockObject)
            {
                _summaryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                
                // Generate final summary if there's pending data
                if (_totalEventsProcessed > 0)
                {
                    GenerateSummary(null);
                }
                
                _logger.LogInformation("EventBatchingService stopped");
            }
        }

        /// <summary>
        /// Adds a keyboard event to the current batch
        /// </summary>
        public void AddKeyboardEvent(int virtualKeyCode, DateTime timestamp)
        {
            lock (_lockObject)
            {
                _keystrokeBuffer.Add(virtualKeyCode);
                _keystrokeTimes.Add(timestamp);
                _totalEventsProcessed++;
                
                _logger.LogDebug("Added keyboard event: {Key}", virtualKeyCode);
            }
        }

        /// <summary>
        /// Adds a mouse event to the current batch
        /// </summary>
        public void AddMouseEvent(MouseEventType eventType, int x, int y, DateTime timestamp)
        {
            lock (_lockObject)
            {
                _mouseEvents.Add(new MouseEventData
                {
                    EventType = eventType,
                    X = x,
                    Y = y,
                    Timestamp = timestamp
                });
                _totalEventsProcessed++;
                
                _logger.LogDebug("Added mouse event: {Type} at ({X}, {Y})", eventType, x, y);
            }
        }

        /// <summary>
        /// Timer callback that generates and fires event summaries
        /// </summary>
        private void GenerateSummary(object? state)
        {
            lock (_lockObject)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var summary = new EventSummary
                    {
                        IntervalStart = _currentBatchStart,
                        IntervalEnd = now,
                        TotalEventsProcessed = _totalEventsProcessed
                    };

                    // Process keyboard events
                    if (_keystrokeBuffer.Count > 0)
                    {
                        summary.KeyboardActivity = CreateKeyboardSummary();
                    }

                    // Process mouse events
                    if (_mouseEvents.Count > 0)
                    {
                        summary.MouseActivity = CreateMouseSummary();
                    }

                    // Only fire event if there was activity or this is a forced summary
                    if (_totalEventsProcessed > 0 || state == null)
                    {
                        SummaryReady?.Invoke(this, summary);
                        _logger.LogInformation("Generated summary: {Description}", summary.GetSummaryDescription());
                    }

                    // Reset for next batch
                    ResetBatch(now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating event summary");
                }
            }
        }

        /// <summary>
        /// Creates a keyboard activity summary from buffered keystrokes
        /// </summary>
        private KeyboardSummary CreateKeyboardSummary()
        {
            var summary = new KeyboardSummary
            {
                KeystrokeCount = _keystrokeBuffer.Count
            };

            // Attempt to reconstruct typed text
            var textBuilder = new StringBuilder();
            foreach (var key in _keystrokeBuffer)
            {
                var character = ConvertKeyToChar(key);
                if (character.HasValue)
                {
                    textBuilder.Append(character.Value);
                }
            }

            summary.TextTyped = textBuilder.ToString();

            // Calculate typing speed
            if (_keystrokeTimes.Count > 1)
            {
                var duration = _keystrokeTimes.Last() - _keystrokeTimes.First();
                if (duration.TotalMinutes > 0)
                {
                    summary.TypingSpeed = _keystrokeBuffer.Count / duration.TotalMinutes;
                }
            }

            return summary;
        }

        /// <summary>
        /// Creates a mouse activity summary from buffered mouse events
        /// </summary>
        private MouseSummary CreateMouseSummary()
        {
            var summary = new MouseSummary();
            var movementEvents = _mouseEvents.Where(e => e.EventType == MouseEventType.Move).ToList();
            var clickEvents = _mouseEvents.Where(e => e.EventType != MouseEventType.Move).ToList();

            summary.ClickCount = clickEvents.Count;
            summary.MovementEvents = movementEvents.Count;

            // Calculate total movement distance
            if (movementEvents.Count > 1)
            {
                double totalDistance = 0;
                for (int i = 1; i < movementEvents.Count; i++)
                {
                    var prev = movementEvents[i - 1];
                    var curr = movementEvents[i];
                    var distance = Math.Sqrt(Math.Pow(curr.X - prev.X, 2) + Math.Pow(curr.Y - prev.Y, 2));
                    totalDistance += distance;
                }
                summary.TotalDistance = totalDistance;

                // Calculate active movement time (time between first and last movement)
                summary.ActiveMovementTime = movementEvents.Last().Timestamp - movementEvents.First().Timestamp;
            }

            return summary;
        }

        /// <summary>
        /// Converts a virtual key code to a character (best effort)
        /// </summary>
        private char? ConvertKeyToChar(int virtualKeyCode)
        {
            // Handle letters (A=0x41, Z=0x5A)
            if (virtualKeyCode >= 0x41 && virtualKeyCode <= 0x5A)
            {
                return (char)('a' + (virtualKeyCode - 0x41));
            }

            // Handle digits (0=0x30, 9=0x39)
            if (virtualKeyCode >= 0x30 && virtualKeyCode <= 0x39)
            {
                return (char)('0' + (virtualKeyCode - 0x30));
            }

            // Handle numpad digits (NumPad0=0x60, NumPad9=0x69)
            if (virtualKeyCode >= 0x60 && virtualKeyCode <= 0x69)
            {
                return (char)('0' + (virtualKeyCode - 0x60));
            }

            // Handle common symbols and special keys using virtual key codes
            return virtualKeyCode switch
            {
                0x20 => ' ',  // Space
                0x0D => '\n', // Enter
                0x09 => '\t', // Tab
                0xBE => '.',  // Period
                0xBC => ',',  // Comma
                0xBF => '?',  // Question mark (/)
                0xBA => ';',  // Semicolon
                0xDE => '\'', // Quote
                0xDB => '[',  // Left bracket
                0xDD => ']',  // Right bracket
                0xBD => '-',  // Minus
                0xBB => '+',  // Plus (=)
                0x08 => '\b', // Backspace
                _ => null
            };
        }

        /// <summary>
        /// Resets the current batch data
        /// </summary>
        private void ResetBatch(DateTime newBatchStart)
        {
            _currentBatchStart = newBatchStart;
            _keystrokeBuffer.Clear();
            _keystrokeTimes.Clear();
            _mouseEvents.Clear();
            _totalEventsProcessed = 0;
        }

        public void Dispose()
        {
            _summaryTimer?.Dispose();
        }
    }

    /// <summary>
    /// Data structure for storing mouse event information
    /// </summary>
    public class MouseEventData
    {
        public MouseEventType EventType { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Types of mouse events
    /// </summary>
    public enum MouseEventType
    {
        Move,
        LeftClick,
        RightClick,
        MiddleClick,
        Scroll
    }
}
