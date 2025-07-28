using System;
using System.Windows;
using System.Windows.Controls;
using DeskDefender.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Windows
{
    /// <summary>
    /// Settings window for configuring application preferences
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SettingsWindow> _logger;
        private readonly EventCoordinatorService _eventCoordinator;
        private readonly ThemeService _themeService;
        private readonly SettingsService _settingsService;
        
        // Store original values for cancel functionality
        private int _originalBatchingInterval;
        private double _originalMotionSensitivity;
        private ThemeService.Theme _originalTheme;

        public SettingsWindow(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = _serviceProvider.GetRequiredService<ILogger<SettingsWindow>>();
            _eventCoordinator = _serviceProvider.GetRequiredService<EventCoordinatorService>();
            _themeService = _serviceProvider.GetRequiredService<ThemeService>();
            _settingsService = _serviceProvider.GetRequiredService<SettingsService>();
            
            InitializeComponent();
            LoadCurrentSettings();
        }

        /// <summary>
        /// Load current settings into the UI
        /// </summary>
        private void LoadCurrentSettings()
        {
            try
            {
                var settings = _settingsService.Settings;

                // Load batching interval from saved settings
                _originalBatchingInterval = settings.BatchingIntervalSeconds;
                BatchingIntervalSlider.Value = settings.BatchingIntervalSeconds;
                BatchingIntervalValue.Text = $"{settings.BatchingIntervalSeconds} seconds";
                CurrentIntervalText.Text = $"Current interval: {settings.BatchingIntervalSeconds} seconds";

                // Load theme setting from saved settings
                _originalTheme = _themeService.CurrentTheme;
                if (settings.Theme == "Dark")
                {
                    DarkThemeRadio.IsChecked = true;
                }
                else
                {
                    LightThemeRadio.IsChecked = true;
                }

                // Load motion sensitivity from saved settings
                _originalMotionSensitivity = settings.MotionSensitivity;
                MotionSensitivitySlider.Value = settings.MotionSensitivity;
                MotionSensitivityValue.Text = settings.MotionSensitivity.ToString("F1");

                _logger.LogInformation("Settings loaded successfully from persistent storage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load current settings");
                MessageBox.Show("Failed to load current settings. Using default values.", 
                               "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Handle batching interval slider changes
        /// </summary>
        private void BatchingInterval_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BatchingIntervalValue != null)
            {
                var interval = (int)e.NewValue;
                BatchingIntervalValue.Text = $"{interval} seconds";
            }
        }

        /// <summary>
        /// Handle motion sensitivity slider changes
        /// </summary>
        private void MotionSensitivity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MotionSensitivityValue != null)
            {
                var sensitivity = e.NewValue;
                MotionSensitivityValue.Text = sensitivity.ToString("F1");
            }
        }

        /// <summary>
        /// Handle theme radio button changes
        /// </summary>
        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            // Theme changes are applied immediately for preview
            try
            {
                if (DarkThemeRadio?.IsChecked == true)
                {
                    _themeService.ApplyTheme(ThemeService.Theme.Dark);
                }
                else if (LightThemeRadio?.IsChecked == true)
                {
                    _themeService.ApplyTheme(ThemeService.Theme.Light);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme preview");
            }
        }

        /// <summary>
        /// Apply settings without closing the window
        /// </summary>
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
        }

        /// <summary>
        /// Apply settings and close the window
        /// </summary>
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (ApplySettings())
            {
                DialogResult = true;
                Close();
            }
        }

        /// <summary>
        /// Cancel changes and close the window
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Restore original settings
                _eventCoordinator.SetSummaryInterval(_originalBatchingInterval);
                _themeService.ApplyTheme(_originalTheme);
                
                DialogResult = false;
                Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore original settings");
                Close(); // Close anyway to prevent user from being stuck
            }
        }

        /// <summary>
        /// Apply all settings changes
        /// </summary>
        private bool ApplySettings(object sender = null)
        {
            try
            {
                // Apply batching interval
                var newInterval = (int)BatchingIntervalSlider.Value;
                _eventCoordinator.SetSummaryInterval(newInterval);
                _settingsService.Settings.BatchingIntervalSeconds = newInterval;
                _originalBatchingInterval = newInterval;
                CurrentIntervalText.Text = $"Current interval: {newInterval} seconds";
                LastSummaryText.Text = $"Settings applied at: {DateTime.Now:HH:mm:ss}";

                // Apply theme and save to settings
                var newTheme = DarkThemeRadio.IsChecked == true ? "Dark" : "Light";
                _settingsService.Settings.Theme = newTheme;
                _originalTheme = _themeService.CurrentTheme;

                // Apply motion sensitivity
                var newMotionSensitivity = MotionSensitivitySlider.Value;
                _settingsService.Settings.MotionSensitivity = newMotionSensitivity;
                _originalMotionSensitivity = newMotionSensitivity;

                // Save all settings to persistent storage
                _settingsService.SaveSettings();

                _logger.LogInformation("Settings applied and saved successfully");
                
                // Show brief confirmation
                var originalContent = ((Button)sender)?.Content;
                if (sender is Button button)
                {
                    button.Content = "Applied!";
                    
                    // Reset button text after 2 seconds
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(2)
                    };
                    timer.Tick += (s, args) =>
                    {
                        button.Content = originalContent;
                        timer.Stop();
                    };
                    timer.Start();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply settings");
                MessageBox.Show($"Failed to apply settings: {ex.Message}", 
                               "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Handle Escape key to cancel
        /// </summary>
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Cancel_Click(this, new RoutedEventArgs());
            }
            base.OnKeyDown(e);
        }
    }
}
