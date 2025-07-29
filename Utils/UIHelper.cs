using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskDefender.Models.Events;

namespace DeskDefender.Utils
{
    /// <summary>
    /// Centralized UI utility methods and helpers
    /// Extracted from various UI classes to improve code reusability and maintainability
    /// </summary>
    public static class UIHelper
    {
        /// <summary>
        /// Gets the appropriate color brush for event severity levels
        /// </summary>
        public static SolidColorBrush GetSeverityBrush(EventSeverity severity)
        {
            return severity switch
            {
                EventSeverity.Critical => new SolidColorBrush(System.Windows.Media.Colors.Red),
                EventSeverity.High => new SolidColorBrush(System.Windows.Media.Colors.Orange),
                EventSeverity.Medium => new SolidColorBrush(System.Windows.Media.Colors.Yellow),
                EventSeverity.Low => new SolidColorBrush(System.Windows.Media.Colors.Green),
                EventSeverity.Info => new SolidColorBrush(System.Windows.Media.Colors.LightGray),
                _ => new SolidColorBrush(System.Windows.Media.Colors.Gray)
            };
        }

        /// <summary>
        /// Formats time ago display for events
        /// </summary>
        public static string GetTimeAgo(DateTime timestamp)
        {
            var timeSpan = DateTime.Now - timestamp;
            
            return timeSpan switch
            {
                { TotalMinutes: < 1 } => "Just now",
                { TotalMinutes: < 60 } => $"{(int)timeSpan.TotalMinutes}m ago",
                { TotalHours: < 24 } => $"{(int)timeSpan.TotalHours}h ago",
                { TotalDays: < 7 } => $"{(int)timeSpan.TotalDays}d ago",
                _ => timestamp.ToString("MM/dd/yyyy")
            };
        }

        /// <summary>
        /// Creates a formatted string for event details display
        /// </summary>
        public static string FormatEventDetails(EventLog eventLog)
        {
            var details = new StringBuilder();
            details.AppendLine($"Event Type: {eventLog.EventType}");
            details.AppendLine($"Timestamp: {eventLog.Timestamp:yyyy-MM-dd HH:mm:ss}");
            details.AppendLine($"Severity: {eventLog.Severity}");
            details.AppendLine($"Description: {eventLog.Description}");
            
            if (!string.IsNullOrEmpty(eventLog.Details))
            {
                details.AppendLine($"Details: {eventLog.Details}");
            }
            
            if (!string.IsNullOrEmpty(eventLog.Metadata))
            {
                details.AppendLine($"Metadata: {eventLog.Metadata}");
            }
            
            details.AppendLine($"Alert Sent: {(eventLog.AlertSent ? "Yes" : "No")}");
            
            return details.ToString();
        }

        /// <summary>
        /// Safely updates UI element text on the UI thread
        /// </summary>
        public static void SafeUpdateText(TextBlock textBlock, string text)
        {
            if (textBlock?.Dispatcher.CheckAccess() == true)
            {
                textBlock.Text = text;
            }
            else
            {
                textBlock?.Dispatcher.BeginInvoke(() => textBlock.Text = text);
            }
        }

        /// <summary>
        /// Safely updates button content on the UI thread
        /// </summary>
        public static void SafeUpdateButton(System.Windows.Controls.Button button, string content, bool? isEnabled = null)
        {
            if (button?.Dispatcher.CheckAccess() == true)
            {
                button.Content = content;
                if (isEnabled.HasValue)
                    button.IsEnabled = isEnabled.Value;
            }
            else
            {
                button?.Dispatcher.BeginInvoke(() =>
                {
                    button.Content = content;
                    if (isEnabled.HasValue)
                        button.IsEnabled = isEnabled.Value;
                });
            }
        }

        /// <summary>
        /// Safely updates shape fill on the UI thread
        /// </summary>
        public static void SafeUpdateShapeFill(System.Windows.Shapes.Shape shape, System.Windows.Media.Brush fill)
        {
            if (shape?.Dispatcher.CheckAccess() == true)
            {
                shape.Fill = fill;
            }
            else
            {
                shape?.Dispatcher.BeginInvoke(() => shape.Fill = fill);
            }
        }

        /// <summary>
        /// Creates a ComboBoxItem with the specified content
        /// </summary>
        public static ComboBoxItem CreateComboBoxItem(string content, object? tag = null)
        {
            return new ComboBoxItem
            {
                Content = content,
                Tag = tag
            };
        }

        /// <summary>
        /// Gets the content string from a ComboBoxItem safely
        /// </summary>
        public static string? GetComboBoxItemContent(object? selectedItem)
        {
            return (selectedItem as ComboBoxItem)?.Content?.ToString();
        }

        /// <summary>
        /// Shows a temporary success message on a button
        /// </summary>
        public static void ShowTemporaryButtonMessage(System.Windows.Controls.Button button, string message, int durationMs = 2000)
        {
            if (button == null) return;

            var originalContent = button.Content;
            button.Content = message;
            
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(durationMs)
            };
            
            timer.Tick += (s, e) =>
            {
                button.Content = originalContent;
                timer.Stop();
            };
            
            timer.Start();
        }

        /// <summary>
        /// Validates numeric input for text boxes
        /// </summary>
        public static bool IsValidNumericInput(string input, out int value, int min = 0, int max = int.MaxValue)
        {
            value = 0;
            
            if (string.IsNullOrWhiteSpace(input))
                return false;
                
            if (!int.TryParse(input, out value))
                return false;
                
            return value >= min && value <= max;
        }

        /// <summary>
        /// Formats file size for display
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
