using Mp3SyncManager.Models;

namespace Mp3SyncManager.Services.Interfaces;

public interface IDeviceDetectionService
{
    IReadOnlyList<DetectedDevice> GetCurrentDevices();
    void StartMonitoring(TimeSpan pollInterval);
    void StopMonitoring();
    event EventHandler<DevicesChangedEventArgs>? DevicesChanged;
}
