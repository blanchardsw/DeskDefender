# DeskDefender Phase 1 Implementation Documentation

## Overview

DeskDefender is a comprehensive desktop security monitoring application built with WPF and .NET 9.0. Phase 1 focuses on core monitoring and logging capabilities, providing a solid foundation for advanced security features in future phases.

## Architecture Overview

### High-Level Architecture

DeskDefender follows a layered architecture with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│                    UI Layer (WPF)                           │
│                   MainWindow.xaml                           │
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────────┐
│                Service Layer                                │
│  CompositeMonitoringService (Orchestrator)                 │
└─────┬─────────────┬─────────────┬─────────────┬─────────────┘
      │             │             │             │
┌─────▼─────┐ ┌─────▼─────┐ ┌─────▼─────┐ ┌─────▼─────┐
│   Input   │ │  Camera   │ │   Event   │ │   Alert   │
│ Monitoring│ │  Service  │ │  Logger   │ │  Service  │
└───────────┘ └───────────┘ └───────────┘ └───────────┘
      │             │             │             │
┌─────▼─────────────▼─────────────▼─────────────▼─────────────┐
│                    Data Layer                               │
│              SQLite Database + File System                  │
└─────────────────────────────────────────────────────────────┘
```

### Core Components

1. **UI Layer**: WPF-based user interface with real-time monitoring display
2. **Service Layer**: Business logic and monitoring services
3. **Data Layer**: SQLite database for event storage and file system for images

## Design Patterns and Principles

### SOLID Principles Implementation

#### Single Responsibility Principle (SRP)
- **WindowsInputMonitor**: Solely responsible for input monitoring
- **OpenCvCameraService**: Handles only camera operations and motion detection
- **SqliteEventLogger**: Dedicated to event logging and database operations
- **TwilioAlertService**: Focused exclusively on SMS alert functionality

#### Open/Closed Principle (OCP)
- Interface-based design allows extension without modification
- New monitoring services can be added by implementing `IMonitorService`
- Alert services can be extended by implementing `IAlertService`

#### Liskov Substitution Principle (LSP)
- All implementations can be substituted for their interfaces
- `CompositeMonitoringService` works with any `IMonitorService` implementation

#### Interface Segregation Principle (ISP)
- Separate interfaces for different concerns:
  - `IMonitorService`: Basic monitoring operations
  - `IInputMonitor`: Input-specific operations
  - `ICameraService`: Camera-specific operations
  - `IEventLogger`: Event logging operations
  - `IAlertService`: Alert-specific operations

#### Dependency Inversion Principle (DIP)
- High-level modules depend on abstractions, not concretions
- Dependency injection used throughout the application

### Design Patterns Used

#### 1. Observer Pattern
**Purpose**: Loose coupling between event producers and consumers

**Implementation**:
- **Event Publishers**: `WindowsInputMonitor`, `OpenCvCameraService`
- **Event Consumers**: `CompositeMonitoringService`, UI components
- **Events**: `InputDetected`, `MotionDetected`, `FrameCaptured`, `StatusChanged`

```csharp
// Example: Input monitoring events
public event EventHandler<InputEvent> InputDetected;
public event EventHandler<bool> StatusChanged;

// Event firing
InputDetected?.Invoke(this, inputEvent);
StatusChanged?.Invoke(this, true);
```

#### 2. Composite Pattern
**Purpose**: Treat individual monitoring services and composite service uniformly

**Implementation**:
- `CompositeMonitoringService` acts as the composite
- Individual services (`WindowsInputMonitor`, `OpenCvCameraService`) are leaves
- All implement `IMonitorService` interface

```csharp
public class CompositeMonitoringService : IMonitorService
{
    private readonly IInputMonitor _inputMonitor;
    private readonly ICameraService _cameraService;
    
    public void Start()
    {
        _inputMonitor.Start();
        _cameraService.Start();
    }
}
```

#### 3. Template Method Pattern
**Purpose**: Define algorithm skeleton while allowing subclasses to override specific steps

**Implementation**:
- Event processing in `CompositeMonitoringService`
- Database operations in `SqliteEventLogger`

```csharp
private async Task ProcessEventAsync(EventLog eventLog)
{
    // Template method with defined steps
    ValidateEvent(eventLog);
    EnrichEvent(eventLog);
    await LogEvent(eventLog);
    
    if (ShouldTriggerAlert(eventLog))
    {
        await SendAlert(eventLog);
    }
}
```

#### 4. Strategy Pattern
**Purpose**: Encapsulate algorithms and make them interchangeable

**Implementation**:
- Different alert strategies (SMS, Email, etc.)
- Motion detection algorithms
- Event filtering strategies

```csharp
public interface IAlertService
{
    Task SendAlertAsync(string message, string imagePath = null);
    Task<bool> TestConnectionAsync();
    string ServiceName { get; }
}
```

#### 5. Factory Pattern
**Purpose**: Create objects without specifying exact classes

**Implementation**:
- Event object creation
- Service configuration creation

```csharp
private AppSettings LoadAppSettings()
{
    // Factory method for creating configuration
    return new AppSettings
    {
        EnableSMS = false,
        CapturePhotos = true,
        // ... other settings
    };
}
```

#### 6. Decorator Pattern
**Purpose**: Add behavior to objects dynamically

**Implementation**:
- Event enrichment in `CompositeMonitoringService`
- Logging decorators for services

```csharp
private void EnrichEvent(EventLog eventLog)
{
    // Decorate event with additional information
    eventLog.MachineName = Environment.MachineName;
    eventLog.UserName = Environment.UserName;
    eventLog.ProcessId = Environment.ProcessId;
}
```

#### 7. Command Pattern
**Purpose**: Encapsulate requests as objects

**Implementation**:
- UI commands for start/stop monitoring
- Event processing commands

## Technology Stack

### Core Technologies
- **.NET 9.0**: Latest .NET framework for modern C# features
- **WPF (Windows Presentation Foundation)**: Rich desktop UI framework
- **Entity Framework Core**: Object-relational mapping for database operations
- **SQLite**: Lightweight, embedded database for event storage

### Third-Party Libraries
- **OpenCvSharp4**: Computer vision library for camera operations and motion detection
- **Twilio**: SMS service integration for alert notifications
- **Microsoft.Extensions.DependencyInjection**: Dependency injection container
- **Microsoft.Extensions.Hosting**: Application hosting and lifecycle management
- **Microsoft.Extensions.Logging**: Structured logging framework

### Development Tools
- **Visual Studio 2022**: Primary IDE
- **NuGet Package Manager**: Package management
- **Git**: Version control

## Core Modules Implementation

### 1. UI Layer (`MainWindow.xaml` & `MainWindow.xaml.cs`)

**Purpose**: Provide comprehensive user interface for monitoring control and status display

**Key Features**:
- Real-time monitoring status display
- Camera feed preview
- Event log viewer with filtering
- Monitoring controls (start/stop, sensitivity adjustment)
- Statistics dashboard

**Architecture Highlights**:
- MVVM-like data binding with ObservableCollections
- Async UI updates to maintain responsiveness
- Event-driven UI updates from monitoring services

```csharp
// Example: Real-time UI updates
private void UpdateUI()
{
    TimestampText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    bool isMonitoring = _monitoringService.IsRunning;
    
    if (isMonitoring)
    {
        var uptime = DateTime.UtcNow - _monitoringStartTime;
        UptimeText.Text = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }
}
```

### 2. Input Monitoring (`WindowsInputMonitor.cs`)

**Purpose**: Monitor system-wide keyboard and mouse input using Windows low-level hooks

**Key Features**:
- Low-level Windows API hooks for global input capture
- Idle time calculation and tracking
- Input pattern analysis and behavior metrics
- Configurable sensitivity thresholds

**Technical Implementation**:
- Uses Windows API functions: `SetWindowsHookEx`, `CallNextHookEx`, `UnhookWindowsHookEx`
- Thread-safe event handling with proper synchronization
- Comprehensive error handling and logging

```csharp
// Windows API hook implementation
[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
private static extern IntPtr SetWindowsHookEx(int idHook, 
    LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
{
    if (nCode >= 0)
    {
        // Process input event
        ProcessInputEvent(wParam, lParam);
    }
    
    return CallNextHookEx(_hookID, nCode, wParam, lParam);
}
```

### 3. Camera & Motion Detection (`OpenCvCameraService.cs`)

**Purpose**: Capture video frames and detect motion using computer vision algorithms

**Key Features**:
- OpenCV-based camera capture and processing
- Motion detection using frame differencing
- Configurable motion sensitivity
- Background processing with cancellation support
- Frame capture and storage capabilities

**Computer Vision Algorithms**:
- Gaussian blur for noise reduction
- Frame differencing for motion detection
- Morphological operations for noise filtering
- Contour detection for motion area identification

```csharp
// Motion detection algorithm
private bool DetectMotion(Mat currentFrame)
{
    // Convert to grayscale and apply Gaussian blur
    Cv2.CvtColor(currentFrame, _grayFrame, ColorConversionCodes.BGR2GRAY);
    Cv2.GaussianBlur(_grayFrame, _blurredFrame, new Size(21, 21), 0);
    
    // Calculate frame difference
    Cv2.Absdiff(_previousFrame, _blurredFrame, _diffFrame);
    Cv2.Threshold(_diffFrame, _diffFrame, 25, 255, ThresholdTypes.Binary);
    
    // Calculate motion percentage
    var nonZeroPixels = Cv2.CountNonZero(_diffFrame);
    var totalPixels = _diffFrame.Width * _diffFrame.Height;
    var changePercentage = (double)nonZeroPixels / totalPixels * 100;
    
    return changePercentage > _motionThreshold;
}
```

### 4. Event Logging (`SqliteEventLogger.cs`)

**Purpose**: Persist security events to SQLite database with efficient querying and management

**Key Features**:
- Entity Framework Core integration
- Asynchronous database operations
- Event batching for performance optimization
- Automatic database schema management
- Event retention and cleanup policies
- Fallback file logging for reliability

**Database Schema**:
```sql
CREATE TABLE Events (
    Id TEXT PRIMARY KEY,
    Timestamp DATETIME NOT NULL,
    EventType TEXT NOT NULL,
    Description TEXT NOT NULL,
    Severity INTEGER NOT NULL,
    ImagePath TEXT,
    Metadata TEXT,
    AlertSent BOOLEAN DEFAULT 0,
    MachineName TEXT,
    UserName TEXT,
    ProcessId INTEGER,
    Source TEXT NOT NULL
);
```

**Performance Optimizations**:
- Connection pooling and reuse
- Batch operations for high-volume events
- Indexed queries for fast retrieval
- Asynchronous operations to prevent UI blocking

### 5. Alert System (`TwilioAlertService.cs`)

**Purpose**: Send SMS alerts for critical security events using Twilio API

**Key Features**:
- Twilio SMS integration
- Rate limiting to prevent spam
- Message templating and formatting
- Connection testing and validation
- Comprehensive error handling and retry logic

**Rate Limiting Implementation**:
```csharp
private bool IsRateLimited()
{
    var now = DateTime.UtcNow;
    var recentAlerts = _alertHistory.Count(a => 
        (now - a).TotalMinutes <= _rateLimitWindowMinutes);
    
    return recentAlerts >= _maxAlertsPerWindow;
}
```

### 6. Composite Monitoring Service (`CompositeMonitoringService.cs`)

**Purpose**: Orchestrate all monitoring services and coordinate event processing

**Key Features**:
- Service lifecycle management
- Event aggregation and correlation
- Intelligent alerting logic
- Background maintenance tasks
- Comprehensive error handling and recovery

**Service Coordination**:
```csharp
public void Start()
{
    try
    {
        // Start services in dependency order
        var startupTasks = new List<Task>
        {
            Task.Run(() => _inputMonitor.Start()),
            Task.Run(() => _cameraService.Start())
        };
        
        Task.WaitAll(startupTasks.ToArray(), TimeSpan.FromSeconds(10));
        
        _isMonitoring = true;
        StatusChanged?.Invoke(this, true);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to start monitoring services");
        throw;
    }
}
```

## Configuration and Dependency Injection

### Application Configuration (`App.xaml.cs`)

**Purpose**: Bootstrap application with dependency injection and service configuration

**Key Features**:
- Microsoft.Extensions.Hosting integration
- Service registration and configuration
- Database initialization
- Centralized error handling

```csharp
private IHostBuilder CreateHostBuilder()
{
    return Host.CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            // Configuration
            var appSettings = LoadAppSettings();
            services.AddSingleton(appSettings);
            
            // Core Services
            services.AddSingleton<IInputMonitor, WindowsInputMonitor>();
            services.AddSingleton<ICameraService, OpenCvCameraService>();
            services.AddSingleton<IEventLogger, SqliteEventLogger>();
            services.AddSingleton<IAlertService, TwilioAlertService>();
            services.AddSingleton<IMonitorService, CompositeMonitoringService>();
            
            // UI
            services.AddTransient<MainWindow>();
        });
}
```

### Settings Management (`AppSettings.cs`)

**Purpose**: Centralized configuration management

**Configuration Categories**:
- Alert settings (SMS configuration)
- Monitoring settings (input/camera enable flags)
- Sensitivity settings (thresholds and timeouts)
- Storage settings (retention policies, paths)

## Data Models and Events

### Event Hierarchy

```
EventLog (Base Class)
├── InputEvent (Keyboard/Mouse events)
├── CameraEvent (Motion detection events)
├── LoginEvent (Authentication events)
└── UsbEvent (Device connection events)
```

### Event Severity Levels

```csharp
public enum EventSeverity
{
    Info = 0,      // Normal system activity
    Low = 1,       // Minor anomalies
    Medium = 2,    // Suspicious activity
    Warning = 2,   // Potential issues
    High = 3,      // Likely threats
    Critical = 4   // Confirmed breaches
}
```

## Security Considerations

### Data Protection
- Event data stored locally in SQLite database
- Sensitive configuration encrypted (future enhancement)
- Image files stored with restricted access permissions

### Privacy Compliance
- Local data storage (no cloud transmission by default)
- User consent for monitoring activities
- Data retention policies implemented

### Access Control
- Application requires administrative privileges for system hooks
- Database access restricted to application process
- Log files protected with appropriate file permissions

## Performance Optimizations

### Memory Management
- Proper disposal of unmanaged resources (camera, hooks)
- Event object pooling for high-frequency events
- Bounded collections to prevent memory leaks

### CPU Optimization
- Background processing for intensive operations
- Configurable processing intervals
- Efficient computer vision algorithms

### I/O Optimization
- Asynchronous database operations
- Batch processing for event logging
- Efficient file handling for image storage

## Error Handling and Logging

### Comprehensive Error Handling
- Try-catch blocks around all critical operations
- Graceful degradation when services fail
- Automatic recovery mechanisms

### Structured Logging
- Microsoft.Extensions.Logging framework
- Multiple log levels (Debug, Info, Warning, Error)
- Contextual logging with correlation IDs

```csharp
_logger.LogInformation("Motion detected: {MotionLevel}% change at {Timestamp}", 
    motionLevel, DateTime.UtcNow);
```

## Testing Strategy

### Unit Testing Approach
- Interface-based design enables easy mocking
- Dependency injection facilitates test isolation
- Separate test projects for each major component

### Integration Testing
- Database integration tests with in-memory SQLite
- Service integration tests with test doubles
- UI automation tests for critical workflows

## Future Enhancements (Phase 2+)

### Planned Features
- Face recognition for known users
- Network monitoring capabilities
- Advanced AI-based behavior analysis
- Cloud synchronization and remote monitoring
- Mobile companion application

### Architectural Improvements
- Plugin architecture for extensible monitoring
- Microservices architecture for scalability
- Event sourcing for audit trails
- CQRS pattern for read/write separation

## Deployment and Installation

### System Requirements
- Windows 10/11 (64-bit)
- .NET 9.0 Runtime
- Administrative privileges for system hooks
- Camera access permissions

### Installation Process
1. Install .NET 9.0 Runtime
2. Deploy application binaries
3. Configure Twilio credentials (optional)
4. Grant necessary permissions
5. Initialize database schema

## Troubleshooting Guide

### Common Issues
- **Camera Access Denied**: Check camera permissions and antivirus settings
- **Hook Installation Failed**: Ensure administrative privileges
- **Database Connection Error**: Verify file permissions and disk space
- **SMS Alerts Not Working**: Validate Twilio configuration

### Diagnostic Tools
- Comprehensive logging to diagnose issues
- Built-in connection testing for external services
- Performance counters for monitoring resource usage

## Conclusion

Phase 1 of DeskDefender successfully implements a comprehensive desktop security monitoring solution with:

- **Robust Architecture**: Clean separation of concerns with SOLID principles
- **Comprehensive Monitoring**: Input tracking and motion detection
- **Reliable Data Storage**: SQLite-based event logging with retention policies
- **Flexible Alerting**: SMS notifications with rate limiting
- **Modern UI**: WPF-based interface with real-time updates
- **Extensible Design**: Interface-based architecture for future enhancements

The implementation provides a solid foundation for advanced security features in subsequent phases while maintaining high code quality, performance, and reliability standards.

---

**Document Version**: 1.0  
**Last Updated**: January 27, 2025  
**Phase**: 1 (Core Monitoring & Logging)  
**Status**: Implementation Complete, Documentation Finalized
