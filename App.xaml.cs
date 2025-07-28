using System;
using System.IO;
using System.Windows;
using DeskDefender.Data;
using DeskDefender.Interfaces;
using DeskDefender.Models.Configuration;
using DeskDefender.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeskDefender
{
    /// <summary>
    /// Application entry point with dependency injection configuration
    /// Implements the Composition Root pattern for dependency management
    /// Uses Microsoft.Extensions.DependencyInjection for IoC container
    /// </summary>
    public partial class App : Application
    {
        private IHost _host;
        private IServiceProvider _serviceProvider;

        /// <summary>
        /// Application startup - configures dependency injection and services
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Build the dependency injection container
                _host = CreateHostBuilder().Build();
                _serviceProvider = _host.Services;

                // Initialize database
                InitializeDatabase();

                // Create and show main window
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application startup failed: {ex.Message}", "Startup Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        /// <summary>
        /// Application shutdown - ensures proper cleanup of resources
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Stop any running monitoring services
                var monitoringService = _serviceProvider?.GetService<IMonitorService>();
                if (monitoringService?.IsRunning == true)
                {
                    monitoringService.Stop();
                }

                // Dispose of the host
                _host?.Dispose();
            }
            catch (Exception ex)
            {
                // Log error but don't prevent shutdown
                System.Diagnostics.Debug.WriteLine($"Error during shutdown: {ex.Message}");
            }

            base.OnExit(e);
        }

        /// <summary>
        /// Creates and configures the host builder with all services
        /// Implements the Builder pattern for service configuration
        /// </summary>
        private IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Configuration
                    var appSettings = LoadAppSettings();
                    services.AddSingleton(appSettings);

                    // Logging
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.AddDebug();
                        builder.SetMinimumLevel(LogLevel.Information);
                    });

                    // Database
                    services.AddDbContextFactory<SecurityContext>(options =>
                    {
                        var databasePath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory, 
                            "Data", 
                            "security.db");
                        options.UseSqlite($"Data Source={databasePath}");
                    });

                    // Core Services - Register interfaces with implementations
                    services.AddSingleton<IInputMonitor, WindowsInputMonitor>();
                    services.AddSingleton<ICameraService, OpenCvCameraService>();
                    services.AddSingleton<IEventLogger, SqliteEventLogger>();
                    services.AddSingleton<IAlertService, TwilioAlertService>();
                    services.AddSingleton<IMonitorService, CompositeMonitoringService>();

                    // UI
                    services.AddTransient<MainWindow>();
                });
        }

        /// <summary>
        /// Loads application settings from configuration file or creates defaults
        /// Implements the Factory pattern for configuration creation
        /// </summary>
        private AppSettings LoadAppSettings()
        {
            try
            {
                // For Phase 1, use default settings
                // In future phases, this would load from appsettings.json or user config
                return new AppSettings
                {
                    // Alert Settings (disabled by default for Phase 1)
                    EnableSMS = false,
                    PhoneNumber = string.Empty,
                    TwilioAccountSid = string.Empty,
                    TwilioAuthToken = string.Empty,
                    TwilioFromNumber = string.Empty,

                    // Monitoring Settings
                    CapturePhotos = true,
                    EnableInputMonitoring = true,
                    EnableMotionDetection = true,

                    // Sensitivity Settings
                    MotionSensitivity = 0.5,
                    InputSensitivityThreshold = TimeSpan.FromSeconds(30),

                    // Storage Settings
                    LogRetentionDays = 30,
                    LogStoragePath = "Data\\Logs",
                    ImageStoragePath = "Data\\Images"
                };
            }
            catch (Exception ex)
            {
                // Log error and return defaults
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                return new AppSettings();
            }
        }

        /// <summary>
        /// Initializes the database and ensures it's ready for use
        /// Creates tables and applies any necessary migrations
        /// </summary>
        private void InitializeDatabase()
        {
            try
            {
                // Ensure data directory exists
                var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                if (!Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory);
                }

                var contextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<SecurityContext>>();
                using var context = contextFactory.CreateDbContext();
                
                // Test database connection first
                if (!context.Database.CanConnect())
                {
                    // Ensure database is created
                    context.Database.EnsureCreated();
                }
                
                // Verify database is accessible
                var canConnect = context.Database.CanConnect();
                if (!canConnect)
                {
                    throw new InvalidOperationException("Cannot connect to database after creation");
                }
                
                // Log successful initialization
                var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Database initialized successfully at: {DataDirectory}", dataDirectory);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Failed to initialize database: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Inner exception: {ex.InnerException.Message}";
                }
                
                // Try to log to debug output if logger isn't available
                System.Diagnostics.Debug.WriteLine(errorMessage);
                Console.WriteLine(errorMessage);
                
                throw new InvalidOperationException(errorMessage, ex);
            }
        }
    }
}

