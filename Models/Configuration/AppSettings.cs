using System;

namespace DeskDefender.Models.Configuration
{
    /// <summary>
    /// Application configuration settings
    /// </summary>
    public class AppSettings
    {
        // Alert Settings
        public bool EnableSMS { get; set; } = false;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool EnableEmail { get; set; } = false;
        public string EmailAddress { get; set; } = string.Empty;
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;

        // Monitoring Settings
        public bool CapturePhotos { get; set; } = true;
        public bool EnableVoiceRecognition { get; set; } = false;
        public bool EnableInputMonitoring { get; set; } = true;
        public bool EnableMotionDetection { get; set; } = true;
        public bool EnableLoginMonitoring { get; set; } = true;
        public bool EnableUsbMonitoring { get; set; } = true;
        public bool EnableFaceRecognition { get; set; } = false;

        // Sensitivity Settings
        public double MotionSensitivity { get; set; } = 0.5;
        public TimeSpan InputSensitivityThreshold { get; set; } = TimeSpan.FromSeconds(30);
        public double FaceRecognitionConfidenceThreshold { get; set; } = 0.8;

        // Storage Settings
        public bool EncryptLogs { get; set; } = true;
        public int LogRetentionDays { get; set; } = 30;
        public string LogStoragePath { get; set; } = @"Data\Logs";
        public string ImageStoragePath { get; set; } = @"Data\Images";

        // Advanced Features
        public bool EnableBehaviorAnalysis { get; set; } = false;
        public bool EnableBluetoothProximity { get; set; } = false;
        public string BluetoothDeviceName { get; set; } = string.Empty;
        public bool EnableTamperDetection { get; set; } = true;
        public bool EnableRemoteDashboard { get; set; } = false;
        public int RemoteDashboardPort { get; set; } = 8080;

        // AI and Voice Settings
        public string VoiceRecognitionLanguage { get; set; } = "en-US";
        public double VoiceRecognitionSensitivity { get; set; } = 0.7;
        public bool UseAzureSpeechService { get; set; } = false;
        public string AzureSpeechKey { get; set; } = string.Empty;
        public string AzureSpeechRegion { get; set; } = string.Empty;

        // Twilio Settings
        public string TwilioAccountSid { get; set; } = string.Empty;
        public string TwilioAuthToken { get; set; } = string.Empty;
        public string TwilioFromNumber { get; set; } = string.Empty;
    }
}
