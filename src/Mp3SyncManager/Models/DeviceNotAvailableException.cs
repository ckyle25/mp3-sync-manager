namespace Mp3SyncManager.Models;

public class DeviceNotAvailableException : InvalidOperationException
{
    public DeviceNotAvailableException(string deviceRootPath)
        : base($"Device at '{deviceRootPath}' is no longer detected as a connected removable drive.") { }
}
