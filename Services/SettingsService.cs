using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DeskDefender.Interfaces;
using DeskDefender.Models.Settings;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Service for persisting user settings between sessions
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly ILogger<SettingsService> _logger;
        private readonly string _settingsFilePath;
        private readonly string _alertSettingsFilePath;
        private UserSettings _currentSettings;
        private AlertSettings _currentAlertSettings;

        public event EventHandler? SettingsChanged;

        public SettingsService(ILogger<SettingsService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Store settings in AppData folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "DeskDefender");
            Directory.CreateDirectory(appFolder);
            _settingsFilePath = Path.Combine(appFolder, "settings.json");
            _alertSettingsFilePath = Path.Combine(appFolder, "alert_settings.json");
            
            _currentSettings = new UserSettings();
            _currentAlertSettings = new AlertSettings();
        }

        /// <summary>
        /// Current user settings
        /// </summary>
        public UserSettings Settings => _currentSettings;

        /// <summary>
        /// Load settings from file
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var loadedSettings = JsonSerializer.Deserialize<UserSettings>(json);
                    
                    if (loadedSettings != null)
                    {
                        _currentSettings = loadedSettings;
                        _logger.LogInformation("Settings loaded successfully from {FilePath}", _settingsFilePath);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to deserialize settings, using defaults");
                        _currentSettings = new UserSettings();
                    }
                }
                else
                {
                    _logger.LogInformation("Settings file not found, using default settings");
                    _currentSettings = new UserSettings();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings from {FilePath}, using defaults", _settingsFilePath);
                _currentSettings = new UserSettings();
            }
        }

        /// <summary>
        /// Save settings to file
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                var json = JsonSerializer.Serialize(_currentSettings, options);
                File.WriteAllText(_settingsFilePath, json);
                
                _logger.LogInformation("Settings saved successfully to {FilePath}", _settingsFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings to {FilePath}", _settingsFilePath);
                throw;
            }
        }

        /// <summary>
        /// Update a specific setting and save
        /// </summary>
        public void UpdateSetting<T>(string settingName, T value)
        {
            try
            {
                var property = typeof(UserSettings).GetProperty(settingName);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(_currentSettings, value);
                    SaveSettings();
                    _logger.LogDebug("Updated setting {SettingName} to {Value}", settingName, value);
                }
                else
                {
                    _logger.LogWarning("Setting {SettingName} not found or not writable", settingName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update setting {SettingName}", settingName);
                throw;
            }
        }

        /// <summary>
        /// Reset settings to defaults
        /// </summary>
        public void ResetToDefaults()
        {
            try
            {
                _currentSettings = new UserSettings();
                _currentAlertSettings = new AlertSettings();
                SaveSettings();
                SaveAlertSettingsAsync(_currentAlertSettings).Wait();
                _logger.LogInformation("Settings reset to defaults");
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset settings to defaults");
                throw;
            }
        }

        /// <summary>
        /// Gets alert settings for SMS and email notifications
        /// </summary>
        public async Task<AlertSettings> GetAlertSettingsAsync()
        {
            try
            {
                await LoadAlertSettingsAsync();
                return _currentAlertSettings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get alert settings");
                return new AlertSettings();
            }
        }

        /// <summary>
        /// Saves alert settings for SMS and email notifications
        /// </summary>
        public async Task SaveAlertSettingsAsync(AlertSettings alertSettings)
        {
            try
            {
                _currentAlertSettings = alertSettings ?? throw new ArgumentNullException(nameof(alertSettings));
                
                var json = JsonSerializer.Serialize(_currentAlertSettings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                await File.WriteAllTextAsync(_alertSettingsFilePath, json);
                _logger.LogInformation("Alert settings saved successfully");
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save alert settings");
                throw;
            }
        }

        /// <summary>
        /// Load alert settings from file
        /// </summary>
        private async Task LoadAlertSettingsAsync()
        {
            try
            {
                if (!File.Exists(_alertSettingsFilePath))
                {
                    _logger.LogDebug("Alert settings file not found, using defaults");
                    _currentAlertSettings = new AlertSettings();
                    return;
                }

                var json = await File.ReadAllTextAsync(_alertSettingsFilePath);
                var settings = JsonSerializer.Deserialize<AlertSettings>(json);
                _currentAlertSettings = settings ?? new AlertSettings();
                
                _logger.LogDebug("Alert settings loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load alert settings, using defaults");
                _currentAlertSettings = new AlertSettings();
            }
        }

        /// <summary>
        /// Gets the current user settings
        /// </summary>
        public UserSettings GetSettings()
        {
            return _currentSettings;
        }

        /// <summary>
        /// Saves user settings
        /// </summary>
        public void SaveSettings(UserSettings settings)
        {
            _currentSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            SaveSettings();
        }

        /// <summary>
        /// Gets the monitoring interval in seconds
        /// </summary>
        public int BatchingIntervalSeconds => _currentSettings.BatchingIntervalSeconds;
    }

    /// <summary>
    /// User settings data model
    /// </summary>
    public class UserSettings
    {
        /// <summary>
        /// Event batching interval in seconds
        /// </summary>
        public int BatchingIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// Selected theme (Dark or Light)
        /// </summary>
        public string Theme { get; set; } = "Dark";

        /// <summary>
        /// Motion detection sensitivity (0.0 to 1.0)
        /// </summary>
        public double MotionSensitivity { get; set; } = 0.5;

        /// <summary>
        /// Whether to start monitoring automatically on app startup
        /// </summary>
        public bool AutoStartMonitoring { get; set; } = false;

        /// <summary>
        /// Maximum number of events to keep in memory
        /// </summary>
        public int MaxEventsInMemory { get; set; } = 1000;

        /// <summary>
        /// Number of days to keep events in database before auto-purge
        /// </summary>
        public int EventRetentionDays { get; set; } = 30;

        /// <summary>
        /// Maximum database size in MB before warning user
        /// </summary>
        public int MaxDatabaseSizeMB { get; set; } = 100;

        /// <summary>
        /// Window position and size settings
        /// </summary>
        public WindowSettings Window { get; set; } = new WindowSettings();
    }

    /// <summary>
    /// Window position and size settings
    /// </summary>
    public class WindowSettings
    {
        public double Left { get; set; } = 100;
        public double Top { get; set; } = 100;
        public double Width { get; set; } = 1200;
        public double Height { get; set; } = 700;
        public bool IsMaximized { get; set; } = false;
    }
}
