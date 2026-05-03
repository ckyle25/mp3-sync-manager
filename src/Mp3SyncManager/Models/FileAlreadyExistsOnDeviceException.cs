namespace Mp3SyncManager.Models;

public class FileAlreadyExistsOnDeviceException : InvalidOperationException
{
    public FileAlreadyExistsOnDeviceException(string fileName)
        : base($"'{fileName}' already exists on the device.") { }
}
