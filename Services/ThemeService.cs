using System;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace DeskDefender.Services
{
    /// <summary>
    /// Service for managing application themes
    /// </summary>
    public class ThemeService
    {
        private readonly ILogger<ThemeService> _logger;
        private const string DarkThemeUri = "Themes/DarkTheme.xaml";
        private const string LightThemeUri = "Themes/LightTheme.xaml";
        
        public ThemeService(ILogger<ThemeService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Available theme options
        /// </summary>
        public enum Theme
        {
            Dark,
            Light
        }

        /// <summary>
        /// Current active theme
        /// </summary>
        public Theme CurrentTheme { get; private set; } = Theme.Dark;

        /// <summary>
        /// Event fired when theme changes
        /// </summary>
        public event Action<Theme> ThemeChanged;

        /// <summary>
        /// Apply the specified theme to the application
        /// </summary>
        /// <param name="theme">Theme to apply</param>
        public void ApplyTheme(Theme theme)
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app == null)
                {
                    _logger.LogWarning("Cannot apply theme: System.Windows.Application.Current is null");
                    return;
                }

                // Remove existing theme resources
                RemoveExistingThemeResources(app);

                // Add new theme resources
                var themeUri = theme == Theme.Dark ? DarkThemeUri : LightThemeUri;
                var themeResourceDict = new ResourceDictionary
                {
                    Source = new Uri(themeUri, UriKind.Relative)
                };

                app.Resources.MergedDictionaries.Add(themeResourceDict);

                CurrentTheme = theme;
                _logger.LogInformation("Applied {Theme} theme successfully", theme);

                // Notify listeners of theme change
                ThemeChanged?.Invoke(theme);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply {Theme} theme", theme);
                throw;
            }
        }

        /// <summary>
        /// Toggle between dark and light themes
        /// </summary>
        public void ToggleTheme()
        {
            var newTheme = CurrentTheme == Theme.Dark ? Theme.Light : Theme.Dark;
            ApplyTheme(newTheme);
        }

        /// <summary>
        /// Initialize theme system with default theme
        /// </summary>
        public void Initialize()
        {
            try
            {
                // Apply dark theme as default
                ApplyTheme(Theme.Dark);
                _logger.LogInformation("Theme system initialized with Dark theme as default");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize theme system");
                throw;
            }
        }

        /// <summary>
        /// Remove existing theme resource dictionaries
        /// </summary>
        private void RemoveExistingThemeResources(System.Windows.Application app)
        {
            try
            {
                var resourcesToRemove = new System.Collections.Generic.List<ResourceDictionary>();

                foreach (var resourceDict in app.Resources.MergedDictionaries)
                {
                    if (resourceDict.Source != null)
                    {
                        var sourceString = resourceDict.Source.ToString();
                        if (sourceString.Contains("DarkTheme.xaml") || sourceString.Contains("LightTheme.xaml"))
                        {
                            resourcesToRemove.Add(resourceDict);
                        }
                    }
                }

                foreach (var resourceDict in resourcesToRemove)
                {
                    app.Resources.MergedDictionaries.Remove(resourceDict);
                }

                _logger.LogDebug("Removed {Count} existing theme resources", resourcesToRemove.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error removing existing theme resources");
            }
        }

        /// <summary>
        /// Get theme display name for UI
        /// </summary>
        /// <param name="theme">Theme to get display name for</param>
        /// <returns>Human-readable theme name</returns>
        public string GetThemeDisplayName(Theme theme)
        {
            return theme switch
            {
                Theme.Dark => "Dark Theme",
                Theme.Light => "Light Theme",
                _ => "Unknown Theme"
            };
        }
    }
}
