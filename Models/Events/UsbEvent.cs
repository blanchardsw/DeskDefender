using System;

namespace DeskDefender.Models.Events
{
    /// <summary>
    /// Event model for USB device connection/disconnection events
    /// </summary>
    public class UsbEvent : EventLog
    {
        public UsbEvent()
        {
            EventType = "USB";
            Source = "UsbMonitor";
        }

        /// <summary>
        /// Name/description of the USB device
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// Whether the device was connected (true) or disconnected (false)
        /// </summary>
        public bool Connected { get; set; }

        /// <summary>
        /// Device ID from Windows
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// Vendor ID of the USB device
        /// </summary>
        public string VendorId { get; set; }

        /// <summary>
        /// Product ID of the USB device
        /// </summary>
        public string ProductId { get; set; }

        /// <summary>
        /// Drive letter assigned to the device (if applicable)
        /// </summary>
        public string DriveLetter { get; set; }

        /// <summary>
        /// Device type (Storage, HID, etc.)
        /// </summary>
        public string DeviceType { get; set; }
    }
}
