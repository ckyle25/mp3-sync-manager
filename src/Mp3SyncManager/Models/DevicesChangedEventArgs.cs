namespace Mp3SyncManager.Models;

public class DevicesChangedEventArgs : EventArgs
{
    public IReadOnlyList<DetectedDevice> Devices { get; }

    public DevicesChangedEventArgs(IReadOnlyList<DetectedDevice> devices)
    {
        Devices = devices;
    }
}
