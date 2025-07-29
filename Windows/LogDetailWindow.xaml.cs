using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskDefender.Models.Events;

namespace DeskDefender.Windows
{
    /// <summary>
    /// Modal window for displaying full event log details
    /// </summary>
    public partial class LogDetailWindow : Window
    {
        private readonly EventDisplayModel _eventModel;

        public LogDetailWindow(EventDisplayModel eventModel)
        {
            _eventModel = eventModel ?? throw new ArgumentNullException(nameof(eventModel));
            InitializeComponent();
            LoadEventDetails();
        }

        /// <summary>
        /// Load event details into the UI
        /// </summary>
        private void LoadEventDetails()
        {
            try
            {
                // Set basic event information
                EventTypeText.Text = _eventModel.EventType ?? "Unknown";
                TimestampText.Text = _eventModel.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                FullDescriptionText.Text = _eventModel.Description ?? "No description available";
                AlertStatusText.Text = _eventModel.AlertSent ? "Yes" : "No";

                // Set severity indicator
                SeverityIndicator.Fill = _eventModel.SeverityColor;
                SeverityText.Text = GetSeverityDisplayName(_eventModel.SeverityColor as SolidColorBrush);

                // Set window title with event type
                Title = $"Event Details - {_eventModel.EventType}";
            }
            catch (Exception ex)
            {
                // Handle any errors gracefully
                FullDescriptionText.Text = $"Error loading event details: {ex.Message}";
            }
        }

        /// <summary>
        /// Get display name for severity based on color
        /// </summary>
        private string GetSeverityDisplayName(SolidColorBrush severityBrush)
        {
            if (severityBrush?.Color == System.Windows.Media.Colors.Red)
                return "Critical";
            else if (severityBrush?.Color == System.Windows.Media.Colors.Yellow)
                return "Medium";
            else if (severityBrush?.Color == System.Windows.Media.Colors.Green)
                return "Low";
            else if (severityBrush?.Color == System.Windows.Media.Colors.Gray)
                return "Info";
            else
                return "Unknown";
        }

        /// <summary>
        /// Copy event details to clipboard
        /// </summary>
        private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var clipboardText = $"Event Type: {EventTypeText.Text}\n" +
                                   $"Timestamp: {TimestampText.Text}\n" +
                                   $"Severity: {SeverityText.Text}\n" +
                                   $"Alert Sent: {AlertStatusText.Text}\n" +
                                   $"Description:\n{FullDescriptionText.Text}";

                System.Windows.Clipboard.SetText(clipboardText);
                
                // Show brief confirmation
                var originalContent = ((System.Windows.Controls.Button)sender).Content;
                ((System.Windows.Controls.Button)sender).Content = "Copied!";
                
                // Reset button text after 2 seconds
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, args) =>
                {
                    ((System.Windows.Controls.Button)sender).Content = originalContent;
                    timer.Stop();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error", 
                               System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Close the modal window
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Handle Escape key to close window
        /// </summary>
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
            }
            base.OnKeyDown(e);
        }
    }
}
