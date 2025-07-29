using System;
using System.Drawing;
using DeskDefender.Models.Events;
using Microsoft.EntityFrameworkCore;

namespace DeskDefender.Data
{
    /// <summary>
    /// Entity Framework database context for security monitoring data
    /// Implements the Repository pattern through Entity Framework
    /// Provides abstraction layer for data access operations
    /// Uses SQLite as the underlying database provider
    /// </summary>
    public class SecurityContext : DbContext
    {
        #region DbSets

        /// <summary>
        /// Database set for security events
        /// Provides CRUD operations for all event types
        /// </summary>
        public DbSet<EventLog> Events { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the SecurityContext
        /// </summary>
        public SecurityContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the SecurityContext with options
        /// Used by dependency injection container
        /// </summary>
        /// <param name="options">Database context options</param>
        public SecurityContext(DbContextOptions<SecurityContext> options) : base(options)
        {
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Configures the database connection and options
        /// Sets up SQLite as the database provider if not already configured
        /// </summary>
        /// <param name="optionsBuilder">Options builder for database configuration</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Default SQLite configuration
                var databasePath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, 
                    "Data", 
                    "security.db");
                
                // Ensure directory exists
                var directory = System.IO.Path.GetDirectoryName(databasePath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                optionsBuilder.UseSqlite($"Data Source={databasePath}");
                
                // Enable sensitive data logging in debug mode
                #if DEBUG
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.LogTo(Console.WriteLine);
                #endif
            }
        }

        /// <summary>
        /// Configures entity models and relationships
        /// Implements the Fluent API for advanced configuration
        /// </summary>
        /// <param name="modelBuilder">Model builder for entity configuration</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure EventLog entity
            modelBuilder.Entity<EventLog>(entity =>
            {
                // Primary key configuration
                entity.HasKey(e => e.Id);
                
                // Property configurations
                entity.Property(e => e.Id)
                    .IsRequired()
                    .ValueGeneratedNever(); // We generate GUIDs manually

                entity.Property(e => e.Timestamp)
                    .IsRequired()
                    .HasColumnType("datetime");

                entity.Property(e => e.EventType)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Description)
                    .HasMaxLength(1000);

                entity.Property(e => e.Severity)
                    .IsRequired()
                    .HasConversion<int>(); // Store enum as integer

                entity.Property(e => e.ImagePath)
                    .HasMaxLength(500);

                entity.Property(e => e.Metadata)
                    .HasColumnType("text"); // Store JSON as text

                entity.Property(e => e.AlertSent)
                    .IsRequired()
                    .HasDefaultValue(false);

                entity.Property(e => e.IsAlert)
                    .IsRequired()
                    .HasDefaultValue(false);

                entity.Property(e => e.Details)
                    .HasMaxLength(2000);

                entity.Property(e => e.Source)
                    .HasMaxLength(100);

                // Indexes for performance optimization
                entity.HasIndex(e => e.Timestamp)
                    .HasDatabaseName("IX_Events_Timestamp");

                entity.HasIndex(e => e.EventType)
                    .HasDatabaseName("IX_Events_EventType");

                entity.HasIndex(e => e.Severity)
                    .HasDatabaseName("IX_Events_Severity");

                entity.HasIndex(e => new { e.Timestamp, e.EventType })
                    .HasDatabaseName("IX_Events_Timestamp_EventType");
            });

            modelBuilder.Entity<InputEvent>(entity =>
            {
                entity.ToTable("InputEvents");
                
                // Ignore ActivityData property - it's for in-memory use only
                entity.Ignore(e => e.ActivityData);
            });

            modelBuilder.Entity<CameraEvent>(entity =>
            {
                entity.ToTable("CameraEvents");
                
                // Configure Size type conversion for FrameResolution
                entity.Property(e => e.FrameResolution)
                    .HasConversion(
                        size => $"{size.Width},{size.Height}", // Convert Size to string
                        value => ParseSize(value) // Convert string back to Size
                    )
                    .HasMaxLength(20);
                    
                entity.Property(e => e.DetectionType)
                    .HasConversion<int>();
            });

            modelBuilder.Entity<LoginEvent>(entity =>
            {
                entity.ToTable("LoginEvents");
            });

            modelBuilder.Entity<UsbEvent>(entity =>
            {
                entity.ToTable("UsbEvents");
            });
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Parses a string representation of Size back to Size object
        /// Used for Entity Framework type conversion
        /// </summary>
        /// <param name="value">String in format "width,height"</param>
        /// <returns>Size object</returns>
        private static Size ParseSize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new Size(0, 0);
                
            try
            {
                var parts = value.Split(',');
                if (parts.Length == 2 && 
                    int.TryParse(parts[0], out int width) && 
                    int.TryParse(parts[1], out int height))
                {
                    return new Size(width, height);
                }
            }
            catch
            {
                // Fall through to default
            }
            
            return new Size(0, 0);
        }

        /// <summary>
        /// Ensures the database is created and up to date
        /// Applies any pending migrations automatically
        /// </summary>
        public void EnsureCreated()
        {
            try
            {
                Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create database", ex);
            }
        }

        /// <summary>
        /// Gets database statistics for monitoring and diagnostics
        /// </summary>
        /// <returns>Database statistics object</returns>
        public DatabaseStatistics GetStatistics()
        {
            try
            {
                var totalEvents = Events.Count();
                var todayEvents = Events.Count(e => e.Timestamp.Date == DateTime.Now.Date);
                var highSeverityEvents = Events.Count(e => e.Severity >= EventSeverity.High);
                
                var oldestEvent = Events.OrderBy(e => e.Timestamp).FirstOrDefault()?.Timestamp;
                var newestEvent = Events.OrderByDescending(e => e.Timestamp).FirstOrDefault()?.Timestamp;

                return new DatabaseStatistics
                {
                    TotalEvents = totalEvents,
                    TodayEvents = todayEvents,
                    HighSeverityEvents = highSeverityEvents,
                    OldestEventDate = oldestEvent,
                    NewestEventDate = newestEvent,
                    DatabaseSizeBytes = GetDatabaseSize()
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to get database statistics", ex);
            }
        }

        /// <summary>
        /// Gets the size of the database file in bytes
        /// </summary>
        private long GetDatabaseSize()
        {
            try
            {
                var connectionString = Database.GetConnectionString();
                if (connectionString?.Contains("Data Source=") == true)
                {
                    var dataSourceStart = connectionString.IndexOf("Data Source=") + "Data Source=".Length;
                    var dataSourceEnd = connectionString.IndexOf(';', dataSourceStart);
                    if (dataSourceEnd == -1) dataSourceEnd = connectionString.Length;
                    
                    var dbPath = connectionString.Substring(dataSourceStart, dataSourceEnd - dataSourceStart);
                    
                    if (System.IO.File.Exists(dbPath))
                    {
                        return new System.IO.FileInfo(dbPath).Length;
                    }
                }
                
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }

    /// <summary>
    /// Database statistics for monitoring and diagnostics
    /// </summary>
    public class DatabaseStatistics
    {
        public int TotalEvents { get; set; }
        public int TodayEvents { get; set; }
        public int HighSeverityEvents { get; set; }
        public DateTime? OldestEventDate { get; set; }
        public DateTime? NewestEventDate { get; set; }
        public long DatabaseSizeBytes { get; set; }
    }
}
