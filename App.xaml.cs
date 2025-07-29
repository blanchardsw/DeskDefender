using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
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
    public partial class App : System.Windows.Application
    {
        private IHost _host;
        private IServiceProvider _serviceProvider;
        
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();
        
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FreeConsole();

        /// <summary>
        /// Application startup - configures dependency injection and services
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            // Allocate console for debug output in WPF app
            AllocConsole();
            
            // Set up global exception handlers with persistent crash logging
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            
            try
            {
                Console.WriteLine("[DEBUG] Starting DeskDefender application...");
                System.Diagnostics.Debug.WriteLine("[DEBUG] Starting DeskDefender application...");
                
                // Build the dependency injection container
                Console.WriteLine("[DEBUG] Building dependency injection container...");
                _host = CreateHostBuilder().Build();
                _serviceProvider = _host.Services;
                Console.WriteLine("[DEBUG] Dependency injection container built successfully.");

                // Initialize database
                Console.WriteLine("[DEBUG] Initializing database...");
                InitializeDatabase();
                Console.WriteLine("[DEBUG] Database initialized successfully.");

                // Start event coordination system
                Console.WriteLine("[DEBUG] Starting event coordination system...");
                var eventCoordinator = _serviceProvider.GetRequiredService<EventCoordinatorService>();
                eventCoordinator.Start();
                Console.WriteLine("[DEBUG] Event coordination system started successfully.");

                // Initialize settings service
                Console.WriteLine("[DEBUG] Initializing settings service...");
                var settingsService = _serviceProvider.GetRequiredService<SettingsService>();
                settingsService.LoadSettings();
                Console.WriteLine("[DEBUG] Settings service initialized successfully.");

                // Initialize theme system with saved theme preference
                Console.WriteLine("[DEBUG] Initializing theme system...");
                var themeService = _serviceProvider.GetRequiredService<ThemeService>();
                var savedTheme = settingsService.Settings.Theme == "Light" ? ThemeService.Theme.Light : ThemeService.Theme.Dark;
                themeService.ApplyTheme(savedTheme);
                Console.WriteLine("[DEBUG] Theme system initialized successfully.");

                // Initialize database maintenance service
                Console.WriteLine("[DEBUG] Initializing database maintenance...");
                var maintenanceService = _serviceProvider.GetRequiredService<DatabaseMaintenanceService>();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await maintenanceService.PerformMaintenanceAsync();
                        Console.WriteLine("[DEBUG] Database maintenance completed.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Database maintenance failed: {ex.Message}");
                    }
                });
                Console.WriteLine("[DEBUG] Database maintenance service initialized.");

                // Create and show main window
                Console.WriteLine("[DEBUG] Creating main window...");
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                Console.WriteLine("[DEBUG] Main window created, showing window...");
                mainWindow.Show();
                Console.WriteLine("[DEBUG] Main window shown successfully.");

                base.OnStartup(e);
                Console.WriteLine("[DEBUG] Application startup completed successfully.");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Application startup failed: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\nInner Exception: {ex.InnerException.Message}";
                }
                errorMessage += $"\nStack Trace: {ex.StackTrace}";
                
                Console.WriteLine($"[ERROR] {errorMessage}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] {errorMessage}");
                
                System.Windows.MessageBox.Show(errorMessage, "Startup Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
                // Stop event coordination system
                var eventCoordinator = _serviceProvider?.GetService<EventCoordinatorService>();
                eventCoordinator?.Stop();

                // Stop any running monitoring services
                var monitoringService = _serviceProvider?.GetService<IMonitorService>();
                if (monitoringService?.IsRunning == true)
                {
                    monitoringService.Stop();
                }

                // Dispose of the host
                _host?.Dispose();
                
                // Free console
                FreeConsole();
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
                        builder.SetMinimumLevel(LogLevel.Debug); // Enable debug logging for diagnostics
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
                    services.AddSingleton<EventBatchingService>();
                    services.AddSingleton<EventDisplayService>();
                    services.AddSingleton<EventConfigurationService>();
                    services.AddSingleton<EventCoordinatorService>();
                    services.AddSingleton<ThemeService>();
                    services.AddSingleton<SettingsService>();
                    services.AddSingleton<LogExportService>();
                    services.AddSingleton<DatabaseMaintenanceService>();
                    services.AddSingleton<IInputMonitor, WindowsInputMonitor>(); // Regular input monitoring
                    services.AddSingleton<ICameraService, OpenCvCameraService>();
                    services.AddSingleton<IEventLogger, SqliteEventLogger>();
                    services.AddSingleton<IAlertService, TwilioAlertService>();
                    
                    // Phase 2: Session Lock Detection & Background Monitoring Services
                    services.AddSingleton<ISessionMonitor, WindowsSessionMonitor>();
                    services.AddSingleton<ITrayService, WorkingTrayService>();
                    services.AddSingleton<IBackgroundMonitoringService, BackgroundMonitoringService>();
                    
                    // Phase 3: Login Monitoring Services
                    services.AddSingleton<ILoginMonitor, WindowsLoginMonitor>();
                    
                    // Composite monitoring service (must be registered after all dependencies)
                    services.AddSingleton<IMonitorService>(provider => new CompositeMonitoringService(
                        provider.GetRequiredService<IInputMonitor>(),
                        provider.GetRequiredService<ICameraService>(),
                        provider.GetRequiredService<IEventLogger>(),
                        provider.GetRequiredService<IAlertService>(),
                        provider.GetRequiredService<AppSettings>(),
                        provider.GetRequiredService<ILogger<CompositeMonitoringService>>(),
                        provider.GetRequiredService<ISessionMonitor>(),
                        provider.GetRequiredService<IBackgroundMonitoringService>(),
                        provider.GetRequiredService<ILoginMonitor>(),
                        provider
                    ));

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
                    InputSensitivityThreshold = TimeSpan.FromSeconds(5), // Reduced from 30 to 5 seconds for better responsiveness

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
                Console.WriteLine("[DEBUG] Starting database initialization...");
                
                // Ensure data directory exists
                var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                Console.WriteLine($"[DEBUG] Data directory path: {dataDirectory}");
                
                if (!Directory.Exists(dataDirectory))
                {
                    Console.WriteLine("[DEBUG] Creating data directory...");
                    Directory.CreateDirectory(dataDirectory);
                    Console.WriteLine("[DEBUG] Data directory created successfully.");
                }
                else
                {
                    Console.WriteLine("[DEBUG] Data directory already exists.");
                }

                Console.WriteLine("[DEBUG] Getting database context factory...");
                var contextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<SecurityContext>>();
                
                Console.WriteLine("[DEBUG] Creating database context...");
                using var context = contextFactory.CreateDbContext();
                Console.WriteLine("[DEBUG] Database context created successfully.");
                
                // Ensure database exists and is up to date
                Console.WriteLine("[DEBUG] Ensuring database exists and is up to date...");
                
                // Only create database if it doesn't exist - preserve existing data
                if (!context.Database.CanConnect())
                {
                    Console.WriteLine("[DEBUG] Database doesn't exist, creating new database...");
                    context.Database.EnsureCreated();
                    Console.WriteLine("[DEBUG] New database created successfully.");
                }
                else
                {
                    Console.WriteLine("[DEBUG] Database already exists, checking for pending migrations...");
                    // Apply any pending migrations to update schema without losing data
                    try
                    {
                        context.Database.Migrate();
                        Console.WriteLine("[DEBUG] Database migrations applied successfully.");
                    }
                    catch (Exception migrationEx)
                    {
                        Console.WriteLine($"[WARNING] Migration failed, falling back to EnsureCreated: {migrationEx.Message}");
                        // If migrations fail, ensure database is created (this won't delete existing data)
                        context.Database.EnsureCreated();
                    }
                }
                
                // Verify database is accessible
                Console.WriteLine("[DEBUG] Verifying database accessibility...");
                var canConnect = context.Database.CanConnect();
                if (!canConnect)
                {
                    throw new InvalidOperationException("Cannot connect to database after creation");
                }
                Console.WriteLine("[DEBUG] Database accessibility verified.");
                
                // Log successful initialization
                Console.WriteLine("[DEBUG] Getting logger service...");
                var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Database initialized successfully at: {DataDirectory}", dataDirectory);
                Console.WriteLine("[DEBUG] Database initialization completed successfully.");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Failed to initialize database: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Inner exception: {ex.InnerException.Message}";
                }
                errorMessage += $"\nStack Trace: {ex.StackTrace}";
                
                // Try to log to debug output if logger isn't available
                System.Diagnostics.Debug.WriteLine($"[ERROR] {errorMessage}");
                Console.WriteLine($"[ERROR] {errorMessage}");
                
                throw new InvalidOperationException(errorMessage, ex);
            }
        }

        /// <summary>
        /// Handles unhandled exceptions from AppDomain
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            var crashLog = $"[CRASH] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Unhandled Exception:\n" +
                          $"Message: {exception?.Message ?? "Unknown"}\n" +
                          $"Stack Trace: {exception?.StackTrace ?? "Not available"}\n\n";
            WriteCrashLogToFile(crashLog);
        }

        /// <summary>
        /// Handles unhandled exceptions from WPF Dispatcher
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var crashLog = $"[CRASH] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Dispatcher Exception:\n" +
                          $"Message: {e.Exception.Message}\n" +
                          $"Stack Trace: {e.Exception.StackTrace}\n\n";
            WriteCrashLogToFile(crashLog);
            
            // Mark as handled to prevent app termination for debugging
            e.Handled = true;
        }

        /// <summary>
        /// Writes crash logs to persistent file storage
        /// </summary>
        private void WriteCrashLogToFile(string crashLog)
        {
            try
            {
                var crashLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                               "DeskDefender", "crash_logs.txt");
                
                Directory.CreateDirectory(Path.GetDirectoryName(crashLogPath));
                File.AppendAllText(crashLogPath, crashLog);
            }
            catch { /* Ignore errors in crash logging */ }
        }
    }
}

