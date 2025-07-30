using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DeskDefender.Interfaces;
using DeskDefender.Models.Settings;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Windows
{
    /// <summary>
    /// Window for configuring SMS and email alert settings
    /// </summary>
    public partial class AlertSettingsWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly IAlertService _alertService;
        private readonly ILogger<AlertSettingsWindow> _logger;
        private AlertSettings _currentSettings;

        public AlertSettingsWindow(
            ISettingsService settingsService,
            IAlertService alertService,
            ILogger<AlertSettingsWindow> logger)
        {
            InitializeComponent();
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _currentSettings = new AlertSettings();
            
            Loaded += OnWindowLoaded;
        }

        /// <summary>
        /// Window loaded event handler - loads current settings
        /// </summary>
        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadCurrentSettingsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load alert settings");
                MessageBox.Show($"Failed to load alert settings: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Loads current alert settings and populates UI
        /// </summary>
        private async Task LoadCurrentSettingsAsync()
        {
            try
            {
                _currentSettings = await _settingsService.GetAlertSettingsAsync();
                
                // General settings
                SummaryIntervalSlider.Value = _currentSettings.SummaryIntervalMinutes;
                SetSelectedSeverity(_currentSettings.MinimumAlertSeverity);
                IncludeSystemEventsCheckBox.IsChecked = _currentSettings.IncludeSystemEventsInAlerts;
                MaxEventsTextBox.Text = _currentSettings.MaxEventsPerAlert.ToString();
                
                // SMS settings
                SmsEnabledCheckBox.IsChecked = _currentSettings.SmsAlertsEnabled;
                PhoneNumberTextBox.Text = _currentSettings.PhoneNumber ?? "";
                TwilioAccountSidTextBox.Text = _currentSettings.TwilioAccountSid ?? "";
                TwilioAuthTokenPasswordBox.Password = _currentSettings.TwilioAuthToken ?? "";
                TwilioPhoneNumberTextBox.Text = _currentSettings.TwilioPhoneNumber ?? "";
                
                // Email settings
                EmailEnabledCheckBox.IsChecked = _currentSettings.EmailAlertsEnabled;
                EmailAddressTextBox.Text = _currentSettings.EmailAddress ?? "";
                SmtpServerTextBox.Text = _currentSettings.SmtpServer ?? "";
                SmtpPortTextBox.Text = _currentSettings.SmtpPort.ToString();
                SmtpUsernameTextBox.Text = _currentSettings.SmtpUsername ?? "";
                SmtpPasswordPasswordBox.Password = _currentSettings.SmtpPassword ?? "";
                SmtpUseSslCheckBox.IsChecked = _currentSettings.SmtpUseSsl;
                
                // Update panel states
                OnSmsEnabledChanged(null, null);
                OnEmailEnabledChanged(null, null);
                
                _logger.LogInformation("Alert settings loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load current alert settings");
                throw;
            }
        }

        /// <summary>
        /// Sets the selected severity in the combo box
        /// </summary>
        private void SetSelectedSeverity(string severity)
        {
            foreach (ComboBoxItem item in MinimumSeverityComboBox.Items)
            {
                if (item.Tag?.ToString() == severity)
                {
                    MinimumSeverityComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        /// <summary>
        /// Gets the selected severity from the combo box
        /// </summary>
        private string GetSelectedSeverity()
        {
            var selectedItem = MinimumSeverityComboBox.SelectedItem as ComboBoxItem;
            return selectedItem?.Tag?.ToString() ?? "Medium";
        }

        /// <summary>
        /// SMS enabled checkbox changed event handler
        /// </summary>
        private void OnSmsEnabledChanged(object? sender, RoutedEventArgs? e)
        {
            SmsSettingsPanel.IsEnabled = SmsEnabledCheckBox.IsChecked == true;
        }

        /// <summary>
        /// Email enabled checkbox changed event handler
        /// </summary>
        private void OnEmailEnabledChanged(object? sender, RoutedEventArgs? e)
        {
            EmailSettingsPanel.IsEnabled = EmailEnabledCheckBox.IsChecked == true;
        }

        /// <summary>
        /// Test SMS button click handler
        /// </summary>
        private async void OnTestSmsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                TestSmsButton.IsEnabled = false;
                TestSmsButton.Content = "📱 Sending...";
                
                // Update settings with current UI values
                await UpdateSettingsFromUIAsync();
                
                // Save temporarily to test
                await _settingsService.SaveAlertSettingsAsync(_currentSettings);
                
                // Send test SMS
                var success = await _alertService.SendTestAlertsAsync();
                
                if (success)
                {
                    MessageBox.Show("Test SMS sent successfully! Check your phone.", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to send test SMS. Please check your settings and try again.", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send test SMS");
                MessageBox.Show($"Failed to send test SMS: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TestSmsButton.IsEnabled = true;
                TestSmsButton.Content = "📱 Send Test SMS";
            }
        }

        /// <summary>
        /// Test email button click handler
        /// </summary>
        private async void OnTestEmailClick(object sender, RoutedEventArgs e)
        {
            try
            {
                TestEmailButton.IsEnabled = false;
                TestEmailButton.Content = "📧 Sending...";
                
                // Update settings with current UI values
                await UpdateSettingsFromUIAsync();
                
                // Save temporarily to test
                await _settingsService.SaveAlertSettingsAsync(_currentSettings);
                
                // Send test email
                var success = await _alertService.SendTestAlertsAsync();
                
                if (success)
                {
                    MessageBox.Show("Test email sent successfully! Check your inbox.", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to send test email. Please check your settings and try again.", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send test email");
                MessageBox.Show($"Failed to send test email: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TestEmailButton.IsEnabled = true;
                TestEmailButton.Content = "📧 Send Test Email";
            }
        }

        /// <summary>
        /// Save button click handler
        /// </summary>
        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveButton.IsEnabled = false;
                SaveButton.Content = "💾 Saving...";
                
                // Validate inputs
                if (!ValidateInputs())
                {
                    return;
                }
                
                // Update settings from UI
                await UpdateSettingsFromUIAsync();
                
                // Save settings
                await _settingsService.SaveAlertSettingsAsync(_currentSettings);
                
                // Restart alert service if it's configured
                if (_currentSettings.CanSendSms || _currentSettings.CanSendEmail)
                {
                    await _alertService.StopAsync();
                    await _alertService.StartAsync();
                }
                
                MessageBox.Show("Alert settings saved successfully!", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save alert settings");
                MessageBox.Show($"Failed to save alert settings: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveButton.Content = "💾 Save Settings";
            }
        }

        /// <summary>
        /// Cancel button click handler
        /// </summary>
        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Updates current settings from UI values
        /// </summary>
        private async Task UpdateSettingsFromUIAsync()
        {
            // General settings
            _currentSettings.SummaryIntervalMinutes = (int)SummaryIntervalSlider.Value;
            _currentSettings.MinimumAlertSeverity = GetSelectedSeverity();
            _currentSettings.IncludeSystemEventsInAlerts = IncludeSystemEventsCheckBox.IsChecked == true;
            
            if (int.TryParse(MaxEventsTextBox.Text, out int maxEvents))
            {
                _currentSettings.MaxEventsPerAlert = Math.Max(1, Math.Min(100, maxEvents));
            }
            
            // SMS settings
            _currentSettings.SmsAlertsEnabled = SmsEnabledCheckBox.IsChecked == true;
            _currentSettings.PhoneNumber = PhoneNumberTextBox.Text?.Trim();
            _currentSettings.TwilioAccountSid = TwilioAccountSidTextBox.Text?.Trim();
            _currentSettings.TwilioAuthToken = TwilioAuthTokenPasswordBox.Password?.Trim();
            _currentSettings.TwilioPhoneNumber = TwilioPhoneNumberTextBox.Text?.Trim();
            
            // Email settings
            _currentSettings.EmailAlertsEnabled = EmailEnabledCheckBox.IsChecked == true;
            _currentSettings.EmailAddress = EmailAddressTextBox.Text?.Trim();
            _currentSettings.SmtpServer = SmtpServerTextBox.Text?.Trim();
            _currentSettings.SmtpUsername = SmtpUsernameTextBox.Text?.Trim();
            _currentSettings.SmtpPassword = SmtpPasswordPasswordBox.Password?.Trim();
            _currentSettings.SmtpUseSsl = SmtpUseSslCheckBox.IsChecked == true;
            
            if (int.TryParse(SmtpPortTextBox.Text, out int port))
            {
                _currentSettings.SmtpPort = Math.Max(1, Math.Min(65535, port));
            }
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates user inputs
        /// </summary>
        private bool ValidateInputs()
        {
            // Validate SMS settings if enabled
            if (SmsEnabledCheckBox.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(PhoneNumberTextBox.Text))
                {
                    MessageBox.Show("Phone number is required for SMS alerts.", "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    PhoneNumberTextBox.Focus();
                    return false;
                }
                
                if (string.IsNullOrWhiteSpace(TwilioAccountSidTextBox.Text))
                {
                    MessageBox.Show("Twilio Account SID is required for SMS alerts.", "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    TwilioAccountSidTextBox.Focus();
                    return false;
                }
                
                if (string.IsNullOrWhiteSpace(TwilioAuthTokenPasswordBox.Password))
                {
                    MessageBox.Show("Twilio Auth Token is required for SMS alerts.", "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    TwilioAuthTokenPasswordBox.Focus();
                    return false;
                }
                
                if (string.IsNullOrWhiteSpace(TwilioPhoneNumberTextBox.Text))
                {
                    MessageBox.Show("Twilio Phone Number is required for SMS alerts.", "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    TwilioPhoneNumberTextBox.Focus();
                    return false;
                }
            }
            
            // Validate email settings if enabled
            if (EmailEnabledCheckBox.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(EmailAddressTextBox.Text))
                {
                    MessageBox.Show("Email address is required for email alerts.", "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    EmailAddressTextBox.Focus();
                    return false;
                }
                
                if (string.IsNullOrWhiteSpace(SmtpServerTextBox.Text))
                {
                    MessageBox.Show("SMTP server is required for email alerts.", "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    SmtpServerTextBox.Focus();
                    return false;
                }
                
                if (string.IsNullOrWhiteSpace(SmtpUsernameTextBox.Text))
                {
                    MessageBox.Show("SMTP username is required for email alerts.", "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    SmtpUsernameTextBox.Focus();
                    return false;
                }
                
                if (string.IsNullOrWhiteSpace(SmtpPasswordPasswordBox.Password))
                {
                    MessageBox.Show("SMTP password is required for email alerts.", "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    SmtpPasswordPasswordBox.Focus();
                    return false;
                }
            }
            
            // Validate max events
            if (!int.TryParse(MaxEventsTextBox.Text, out int maxEvents) || maxEvents < 1 || maxEvents > 100)
            {
                MessageBox.Show("Maximum events per alert must be a number between 1 and 100.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                MaxEventsTextBox.Focus();
                return false;
            }
            
            // Validate SMTP port if email is enabled
            if (EmailEnabledCheckBox.IsChecked == true)
            {
                if (!int.TryParse(SmtpPortTextBox.Text, out int port) || port < 1 || port > 65535)
                {
                    MessageBox.Show("SMTP port must be a number between 1 and 65535.", "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    SmtpPortTextBox.Focus();
                    return false;
                }
            }
            
            return true;
        }
    }
}
