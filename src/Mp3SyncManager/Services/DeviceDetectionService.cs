using Avalonia.Threading;
using Mp3SyncManager.Models;
using Mp3SyncManager.Services.Interfaces;

namespace Mp3SyncManager.Services;

public class DeviceDetectionService : IDeviceDetectionService, IDisposable
{
    private Timer? _timer;
    private IReadOnlyList<DetectedDevice> _lastKnownDevices = [];

    public event EventHandler<DevicesChangedEventArgs>? DevicesChanged;

    public IReadOnlyList<DetectedDevice> GetCurrentDevices() =>
        DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
            .Select(d => new DetectedDevice
            {
                DriveLabel = d.VolumeLabel,
                RootPath = d.RootDirectory.FullName,
                AvailableFreeSpaceBytes = d.AvailableFreeSpace,
                TotalSizeBytes = d.TotalSize,
            })
            .ToList()
            .AsReadOnly();

    public void StartMonitoring(TimeSpan pollInterval)
    {
        StopMonitoring();
        _lastKnownDevices = GetCurrentDevices();
        _timer = new Timer(Poll, null, pollInterval, pollInterval);
    }

    public void StopMonitoring()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void Poll(object? _)
    {
        var current = GetCurrentDevices();

        var changed = current.Count != _lastKnownDevices.Count
            || current.Any(d => !_lastKnownDevices.Any(p =>
                p.RootPath == d.RootPath && p.DriveLabel == d.DriveLabel))
            || _lastKnownDevices.Any(d => !current.Any(p =>
                p.RootPath == d.RootPath && p.DriveLabel == d.DriveLabel));

        if (!changed) return;

        _lastKnownDevices = current;

        // Events from the timer thread must be marshaled to the UI thread
        Dispatcher.UIThread.Post(() => DevicesChanged?.Invoke(this, new DevicesChangedEventArgs(current)));
    }

    public void Dispose() => StopMonitoring();
}
