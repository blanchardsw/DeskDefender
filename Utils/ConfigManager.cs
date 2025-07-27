using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DeskDefender.Models.Configuration;

namespace DeskDefender.Utils
{
    /// <summary>
    /// Manages application configuration persistence
    /// </summary>
    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        private static AppSettings _cachedSettings;
        private static readonly object _lock = new object();

        /// <summary>
        /// Loads application settings from file
        /// </summary>
        /// <returns>Application settings instance</returns>
        public static AppSettings LoadSettings()
        {
            lock (_lock)
            {
                if (_cachedSettings != null)
                    return _cachedSettings;

                try
                {
                    if (!File.Exists(ConfigPath))
                    {
                        _cachedSettings = new AppSettings();
                        SaveSettings(_cachedSettings);
                        return _cachedSettings;
                    }

                    var json = File.ReadAllText(ConfigPath);
                    _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        WriteIndented = true
                    }) ?? new AppSettings();

                    return _cachedSettings;
                }
                catch (Exception ex)
                {
                    // Log error and return default settings
                    Console.WriteLine($"Error loading settings: {ex.Message}");
                    _cachedSettings = new AppSettings();
                    return _cachedSettings;
                }
            }
        }

        /// <summary>
        /// Saves application settings to file
        /// </summary>
        /// <param name="settings">Settings to save</param>
        public static void SaveSettings(AppSettings settings)
        {
            lock (_lock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    File.WriteAllText(ConfigPath, json);
                    _cachedSettings = settings;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving settings: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Saves application settings asynchronously
        /// </summary>
        /// <param name="settings">Settings to save</param>
        public static async Task SaveSettingsAsync(AppSettings settings)
        {
            await Task.Run(() => SaveSettings(settings));
        }

        /// <summary>
        /// Resets settings to default values
        /// </summary>
        public static void ResetToDefaults()
        {
            lock (_lock)
            {
                _cachedSettings = new AppSettings();
                SaveSettings(_cachedSettings);
            }
        }

        /// <summary>
        /// Gets the path to the configuration file
        /// </summary>
        public static string GetConfigPath() => ConfigPath;
    }
}
