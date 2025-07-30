# DeskDefender Implementation Plan

## 🎯 Project Overview
DeskDefender is a comprehensive desktop security monitoring application built with WPF/.NET 9.0 that detects unauthorized access and suspicious activity through multiple monitoring channels.

## 🏗️ Architecture Overview

### Core Components
1. **UI Layer (WPF)** - Main window, toggle monitoring, settings panel
2. **Input Monitoring Module** - Mouse/keyboard hooks, activity detection
3. **Camera & Motion Detection Module** - OpenCV integration, face/motion detection
4. **Event Logger** - SQLite/JSON storage with timestamps and metadata
5. **Alert System** - SMS/Email integration with event summaries
6. **Background Services** - Multi-threaded monitoring with UI responsiveness

### Architectural Principles
- **Single Responsibility**: Each class has one focused purpose
- **Open/Closed**: Modules extensible but modification-resistant
- **Liskov Substitution**: Interface-based interchangeable implementations
- **Interface Segregation**: Small, focused interfaces
- **Dependency Inversion**: High-level modules depend on abstractions

## 📋 Implementation Phases

## ✅ Phase 1: Core Foundation & Database Persistence (COMPLETED)
**Goal**: Establish persistent log storage with no data loss between sessions

### Completed Features:
- ✅ **Database Persistence**: Fixed database deletion on startup, logs now persist between sessions
- ✅ **SQLite Event Logger**: Robust event storage with proper schema
- ✅ **Event Export**: Fixed export functionality to work with persisted data
- ✅ **Core UI Framework**: Basic monitoring interface and controls
- ✅ **Input Monitoring**: Mouse/keyboard activity detection
- ✅ **Camera Integration**: Basic webcam functionality
- ✅ **Event Logging Pipeline**: Complete event capture and storage system

## ✅ Phase 2: UI Enhancements & Session Management (COMPLETED)
**Goal**: Improve user experience with enhanced UI, session lock detection, and background monitoring

### Completed Features:
- ✅ **Event Details Pop-up**: Word wrap and detailed event viewing
- ✅ **Event Filters**: Type, severity, and time-based filtering
- ✅ **System Tray Integration**: Minimize-to-tray with context menu
- ✅ **Session Lock Detection**: Windows session state monitoring
- ✅ **Background Monitoring**: Continuous monitoring during screen lock
- ✅ **Administrator Manifest**: Always run as administrator for full functionality
- ✅ **Event Log Deletion**: Menu option to clear logs
- ✅ **Auto-refresh**: Real-time event list updates
- ✅ **Exit Menu Fix**: Proper app exit vs minimize behavior
- ✅ **Notification Removal**: Disabled Windows pop-up notifications for stealth
- ✅ **UI Performance**: Fixed lag on monitoring start/stop
- ✅ **Timestamp Accuracy**: Fixed UTC/local time issues throughout codebase

## ✅ Phase 3: Stealth Capture & Advanced Monitoring (COMPLETED)
**Goal**: Implement stealth webcam and screen capture with intelligent activity detection

### Completed Features:
- ✅ **Stealth Screen Capture**: Silent screenshot capture at user-configured intervals
- ✅ **Stealth Webcam Capture**: Silent photo capture using AForge.NET
- ✅ **Activity-Aware Capture**: Only capture screenshots when user input detected
- ✅ **Session Lock Integration**: No capture during screen lock
- ✅ **SYSTEM-Level Service**: Windows Service for lock screen input monitoring
- ✅ **IPC Communication**: Named Pipes for cross-session data transfer
- ✅ **Login Event Logging**: Comprehensive login attempt tracking
- ✅ **Crash Logging**: Persistent crash logs for debugging
- ✅ **Duplicate Filtering**: In-memory deduplication of events
- ✅ **Controller Refactor**: Modular architecture with EventDisplayController, MonitoringController, etc.
- ✅ **Performance Optimizations**: Efficient database operations, reduced EF Core logging
- ✅ **Bug Fixes**: Fixed Clear Logs freezing, filter issues, build errors
- ✅ **Runtime Verification**: All monitoring features tested and working

## 🚧 Phase 4: SMS and Email Messaging/Alerts (IN PROGRESS)
**Goal**: Implement remote notification system for activity alerts when user is away

### Requirements:
- [ ] **SMS Messaging**: Send text message summaries of security events
- [ ] **Email Messaging**: Send email summaries of security events
- [ ] **Event Summarization**: Aggregate multiple events into single message (no spamming)
- [ ] **Configurable Summary Interval**: User can set how often summaries are sent (separate from monitoring interval)
- [ ] **User Contact Configuration**: Settings for phone number and email address
- [ ] **Toggle Controls**: Independent on/off switches for SMS and email notifications
- [ ] **Settings Integration**: All alerting settings accessible in settings menu
- [ ] **Message Throttling**: Prevent excessive messaging during high activity periods
- [ ] **Priority-Based Alerts**: Critical events may trigger immediate alerts vs summary

### Technical Implementation:
```csharp
public interface IAlertService
{
    Task SendSmsAsync(string phoneNumber, string message);
    Task SendEmailAsync(string emailAddress, string subject, string message);
    Task SendEventSummaryAsync(List<EventLog> events);
}

public class AlertSettings
{
    public bool SmsEnabled { get; set; }
    public bool EmailEnabled { get; set; }
    public string PhoneNumber { get; set; }
    public string EmailAddress { get; set; }
    public int SummaryIntervalMinutes { get; set; } = 15;
    public bool ImmediateAlertsForCritical { get; set; } = true;
}
```

## 🔧 Technical Architecture

### Dependencies & Packages
```xml
<!-- Core Dependencies -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />

<!-- Database -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" />

<!-- Computer Vision -->
<PackageReference Include="OpenCvSharp4" Version="4.9.0.20240103" />
<PackageReference Include="OpenCvSharp4.runtime.win" Version="4.9.0.20240103" />

<!-- Communication -->
<PackageReference Include="Twilio" Version="6.16.1" />

<!-- System Integration -->
<PackageReference Include="System.Management" Version="8.0.0" />
<PackageReference Include="System.Drawing.Common" Version="8.0.0" />
```

### Dependency Injection Configuration
```csharp
// App.xaml.cs
services.AddSingleton<IInputMonitor, WindowsInputMonitor>();
services.AddSingleton<ICameraService, OpenCvCameraService>();
services.AddSingleton<IAlertService, TwilioAlertService>();
services.AddSingleton<IEventLogger, SqliteEventLogger>();
services.AddSingleton<IMonitoringService, CompositeMonitoringService>();
```

### Database Schema
```sql
-- Events table
CREATE TABLE Events (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp DATETIME NOT NULL,
    EventType TEXT NOT NULL,
    Description TEXT,
    ImagePath TEXT,
    Metadata TEXT,
    Severity INTEGER
);
```

## 🔧 Technical Architecture

### Core Interfaces

#### 1.2 Input Monitoring Module
```csharp
public interface IInputMonitor
{
    void StartMonitoring(Action<InputEvent> onInputDetected);
    void StopMonitoring();
}

public class WindowsInputMonitor : IInputMonitor
{
    // Uses SetWindowsHookEx for mouse/keyboard detection
}
```
- [ ] Mouse movement and click detection
- [ ] Keyboard activity monitoring
- [ ] Input event aggregation and filtering
- [ ] Background thread implementation

#### 1.3 Camera & Motion Detection Module
```csharp
public interface ICameraService
{
    void StartCapture(Action<Bitmap> onFrameCaptured);
    void StopCapture();
    Bitmap CaptureFrame();
}

public class OpenCvCameraService : ICameraService
{
    // OpenCV integration for camera access and motion detection
}
```
- [ ] Camera initialization and frame capture
- [ ] Motion detection algorithms
- [ ] Image processing and enhancement
- [ ] Multi-camera support preparation

#### 1.4 Event Logger Implementation
```csharp
public interface IEventLogger
{
    void LogEvent(SecurityEvent securityEvent);
    List<SecurityEvent> GetEvents(DateTime from, DateTime to);
}

public class SqliteEventLogger : IEventLogger
{
    // SQLite database for structured event storage
}
```
- [ ] SQLite database schema design
- [ ] Event model classes (SecurityEvent, InputEvent, CameraEvent)
- [ ] Database context and migrations
- [ ] Event querying and filtering

#### 1.5 Alert System Integration
```csharp
public interface IAlertService
{
    Task SendAlertAsync(string message, string imagePath = null);
}

public class TwilioAlertService : IAlertService
{
    // Twilio SMS integration with image links
}
```
- [ ] Twilio SDK integration
- [ ] SMS message formatting
- [ ] Image upload and secure link generation
- [ ] Alert throttling and deduplication

#### 1.6 Background Services Orchestration
```csharp
public interface IMonitoringService
{
    void StartMonitoring();
    void StopMonitoring();
    bool IsMonitoring { get; }
}

public class CompositeMonitoringService : IMonitoringService
{
    // Coordinates all monitoring services
}
```
- [ ] Service coordination and lifecycle management
- [ ] Thread-safe monitoring state management
- [ ] Error handling and recovery
- [ ] Performance monitoring and optimization

### Phase 2: Session Lock Detection & Background Monitoring
**Goal**: Ensure continuous security monitoring even when Windows screen is locked and implement system tray functionality

#### 2.1 Session State Monitoring
```csharp
public interface ISessionMonitor
{
    event EventHandler<SessionStateChangedEventArgs> SessionStateChanged;
    bool IsSessionLocked { get; }
    void StartMonitoring();
    void StopMonitoring();
}

public class WindowsSessionMonitor : ISessionMonitor
{
    // SystemEvents.SessionSwitch event handling
    // Track lock/unlock events with timestamps
    // Maintain session state persistence
}
```
- [ ] Windows session lock/unlock detection via SystemEvents.SessionSwitch
- [ ] Session state event logging with security context
- [ ] Background service continuity during locked sessions
- [ ] Graceful handling of camera access restrictions during lock

#### 2.2 System Tray Integration
```csharp
public interface ITrayService
{
    void MinimizeToTray();
    void RestoreFromTray();
    void ShowTrayNotification(string title, string message);
}

public class SystemTrayService : ITrayService
{
    // NotifyIcon implementation
    // Context menu with monitoring controls
    // Tray notifications for security events
}
```
- [ ] Minimize to system tray functionality
- [ ] Tray icon with context menu (Show/Hide, Start/Stop Monitoring, Exit)
- [ ] Tray notifications for critical security events
- [ ] Persistent background operation when minimized

#### 2.3 Background Service Architecture
```csharp
public interface IBackgroundMonitoringService
{
    void EnsureContinuousMonitoring();
    void HandleSessionStateChange(SessionState newState);
}

public class BackgroundMonitoringService : IBackgroundMonitoringService
{
    // Coordinate all monitoring services during session changes
    // Maintain service state across lock/unlock cycles
}
```
- [ ] Service coordination during session state changes
- [ ] Input monitoring continuity during locked sessions
- [ ] Database operations persistence during background mode
- [ ] Event correlation between locked and unlocked sessions

### Phase 3: Advanced Security Detection
**Goal**: Add advanced detection capabilities and security monitoring features

#### 3.1 Login Attempt Detection
```csharp
public interface ILoginMonitor
{
    void StartMonitoring(Action<LoginEvent> onLoginEvent);
    void StopMonitoring();
}

public class WindowsLoginMonitor : ILoginMonitor
{
    // Windows Event Log monitoring for login attempts
}
```
- [ ] Windows Event Log integration (Security log, Event ID 4625/4624)
- [ ] Failed login attempt detection
- [ ] Successful login tracking
- [ ] User session correlation

#### 3.2 Screen Capture on Activity
```csharp
public interface IScreenCaptureService
{
    Bitmap CaptureScreen();
    Bitmap CaptureActiveWindow();
    Task<string> SaveScreenshotAsync(Bitmap screenshot);
}

public class ScreenCaptureService : IScreenCaptureService
{
    // Graphics.CopyFromScreen implementation
}
```
- [ ] Full screen capture capability
- [ ] Active window capture
- [ ] Multi-monitor support
- [ ] Screenshot compression and storage

#### 3.3 Idle Time Monitoring
```csharp
public interface IIdleMonitor
{
    TimeSpan GetIdleTime();
    void StartMonitoring(Action<IdleEvent> onIdleStateChanged);
}

public class WindowsIdleMonitor : IIdleMonitor
{
    // GetLastInputInfo WinAPI integration
}
```
- [ ] System idle time calculation
- [ ] Idle threshold configuration
- [ ] Activity pattern analysis
- [ ] Idle/active state transitions

#### 3.4 USB Device Detection
```csharp
public interface IUsbMonitor
{
    void StartMonitoring(Action<UsbEvent> onDeviceChange);
    void StopMonitoring();
}

public class UsbMonitor : IUsbMonitor
{
    // WMI ManagementEventWatcher for USB events
}
```
- [ ] USB device insertion/removal detection
- [ ] Device identification and categorization
- [ ] Whitelist/blacklist functionality
- [ ] Device access logging

#### 3.5 Facial Recognition for Known Users
```csharp
public interface IFaceRecognitionService
{
    bool IsKnownFace(Bitmap image);
    void TrainWithKnownFaces(List<Bitmap> knownFaces);
}

public class OpenCvFaceRecognitionService : IFaceRecognitionService
{
    // Haar cascades or trained model implementation
}
```
- [ ] Face detection using OpenCV
- [ ] Known face training interface
- [ ] Face comparison algorithms
- [ ] False positive reduction

### Phase 4: Advanced Features & UI Polish
**Goal**: Implement sophisticated monitoring and security features plus final UI improvements

#### 4.1 Remote Control & Live Feed
```csharp
public interface IRemoteServer
{
    void Start();
    void Stop();
    string GetServerUrl();
}

public class LocalWebServer : IRemoteServer
{
    // ASP.NET Core or HttpListener web server
}
```
- [ ] Local web server implementation
- [ ] Live camera feed streaming
- [ ] Remote log access interface
- [ ] Secure authentication system

#### 3.2 Tamper Detection
```csharp
public interface IAppIntegrityMonitor
{
    void StartMonitoring();
    void StopMonitoring();
}

public class WatchdogService : IAppIntegrityMonitor
{
    // Process monitoring and restart capability
}
```
- [ ] Application process monitoring
- [ ] Service-based watchdog implementation
- [ ] Auto-restart on termination
- [ ] Tamper attempt logging

#### 3.3 Encrypted Log Storage
```csharp
public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    byte[] EncryptFile(byte[] fileData);
    byte[] DecryptFile(byte[] encryptedData);
}

public class AesEncryptionService : IEncryptionService
{
    // AES encryption for logs and images
}
```
- [ ] AES encryption implementation
- [ ] Key management and storage
- [ ] Encrypted database support
- [ ] Secure image storage

#### 3.4 AI Behavior Analysis
```csharp
public interface IBehaviorAnalyzer
{
    bool IsAnomalous(InputPattern pattern);
    void TrainBaseline(List<InputPattern> normalPatterns);
}

public class SimpleBehaviorAnalyzer : IBehaviorAnalyzer
{
    // Statistical analysis for anomaly detection
}
```
- [ ] Input pattern analysis
- [ ] Baseline behavior training
- [ ] Anomaly detection algorithms
- [ ] Machine learning integration preparation

#### 3.5 Bluetooth Proximity Detection
```csharp
public interface IBluetoothMonitor
{
    bool IsDeviceNearby(string deviceName);
    void StartProximityMonitoring(Action<ProximityEvent> onProximityChanged);
}

public class WindowsBluetoothMonitor : IBluetoothMonitor
{
    // Bluetooth API integration
}
```
- [ ] Bluetooth device discovery
- [ ] Proximity-based alert filtering
- [ ] Device pairing management
- [ ] Signal strength analysis

### Phase 4: Optional Enhancements
**Goal**: Add cloud integration and mobile companion features

#### 4.1 Cloud Sync Integration
- [ ] OneDrive/Azure Blob Storage integration
- [ ] Automatic log and image backup
- [ ] Cross-device synchronization
- [ ] Cloud-based alert delivery

#### 4.2 Mobile App Companion
- [ ] REST API for mobile access
- [ ] Push notification integration
- [ ] Remote monitoring dashboard
- [ ] Mobile alert management

## 🔧 Technical Implementation Details

### Dependencies & Packages
```xml
<!-- Core Dependencies -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />

<!-- Database -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" />

<!-- Computer Vision -->
<PackageReference Include="OpenCvSharp4" Version="4.9.0.20240103" />
<PackageReference Include="OpenCvSharp4.runtime.win" Version="4.9.0.20240103" />

<!-- Communication -->
<PackageReference Include="Twilio" Version="6.16.1" />

<!-- System Integration -->
<PackageReference Include="System.Management" Version="8.0.0" />
<PackageReference Include="System.Drawing.Common" Version="8.0.0" />
```

### Dependency Injection Configuration
```csharp
// Program.cs or App.xaml.cs
services.AddSingleton<IInputMonitor, WindowsInputMonitor>();
services.AddSingleton<ICameraService, OpenCvCameraService>();
services.AddSingleton<IFaceRecognitionService, OpenCvFaceRecognitionService>();
services.AddSingleton<IAlertService, TwilioAlertService>();
services.AddSingleton<IEventLogger, SqliteEventLogger>();
services.AddSingleton<IMonitoringService, CompositeMonitoringService>();
```

### Database Schema
```sql
-- Events table
CREATE TABLE Events (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp DATETIME NOT NULL,
    EventType TEXT NOT NULL,
    Description TEXT,
    ImagePath TEXT,
    Metadata TEXT,
    Severity INTEGER
);

-- Settings table
CREATE TABLE Settings (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
```

## 🚀 Getting Started

### Phase 1 Implementation Order
1. Set up dependency injection container
2. Implement basic UI with monitoring toggle
3. Create input monitoring with Windows hooks
4. Add camera service with OpenCV
5. Implement SQLite event logging
6. Integrate Twilio alert service
7. Coordinate services with background monitoring

### Testing Strategy
- Unit tests for each service interface
- Integration tests for monitoring workflows
- UI automation tests for critical paths
- Performance testing for continuous monitoring
- Security testing for encryption and authentication

## 📊 Success Metrics
- **Phase 1**: Basic monitoring operational with alerts
- **Phase 2**: Enhanced detection with reduced false positives
- **Phase 3**: Advanced features with remote access
- **Phase 4**: Cloud integration and mobile companion

## 🔒 Security Considerations
- Encrypted storage of sensitive data
- Secure API endpoints for remote access
- Input validation and sanitization
- Principle of least privilege for system access
- Regular security audits and updates

---

*This implementation plan provides a structured approach to building DeskDefender with clear phases, technical specifications, and success criteria. Each phase builds upon the previous one, ensuring a solid foundation while allowing for iterative development and testing.*
