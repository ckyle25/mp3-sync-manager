namespace Mp3SyncManager.Models;

public class DetectedDevice
{
    public string DriveLabel { get; init; } = string.Empty;
    public string RootPath { get; init; } = string.Empty;
    public long AvailableFreeSpaceBytes { get; init; }
    public long TotalSizeBytes { get; init; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(DriveLabel) ? RootPath : $"{DriveLabel} ({RootPath})";

    public override string ToString() =>
        string.IsNullOrWhiteSpace(DriveLabel) ? RootPath : $"{DriveLabel} ({RootPath})";
}
