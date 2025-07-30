using System.Threading.Tasks;
using DeskDefender.Models.Settings;
using DeskDefender.Services;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for settings management including alert configuration
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets the current user settings
        /// </summary>
        UserSettings GetSettings();

        /// <summary>
        /// Saves user settings
        /// </summary>
        void SaveSettings(UserSettings settings);

        /// <summary>
        /// Gets alert settings for SMS and email notifications
        /// </summary>
        Task<AlertSettings> GetAlertSettingsAsync();

        /// <summary>
        /// Saves alert settings for SMS and email notifications
        /// </summary>
        Task SaveAlertSettingsAsync(AlertSettings alertSettings);

        /// <summary>
        /// Resets all settings to defaults
        /// </summary>
        void ResetToDefaults();

        /// <summary>
        /// Gets the monitoring interval in seconds
        /// </summary>
        int BatchingIntervalSeconds { get; }

        /// <summary>
        /// Event fired when settings are changed
        /// </summary>
        event System.EventHandler? SettingsChanged;
    }
}
