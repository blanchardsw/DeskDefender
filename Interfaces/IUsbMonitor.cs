using System;
using DeskDefender.Models.Events;

namespace DeskDefender.Interfaces
{
    /// <summary>
    /// Interface for monitoring USB device connections
    /// </summary>
    public interface IUsbMonitor : IMonitorService
    {
        /// <summary>
        /// Event fired when a USB device is connected or disconnected
        /// </summary>
        event EventHandler<UsbEvent> UsbDeviceChanged;
    }
}
