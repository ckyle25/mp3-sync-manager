using Mp3SyncManager.Models;

namespace Mp3SyncManager.Services.Interfaces;

public interface IFileTransferService
{
    IReadOnlyList<MusicFile> ListFiles(string folderPath, string searchPattern = "*.mp3", bool displayRelativePaths = false);

    Task CopyFileAsync(
        string sourceFilePath,
        string sourceFolderPath,
        string deviceRootPath,
        IProgress<TransferProgress>? progress,
        bool overwriteExisting,
        CancellationToken cancellationToken);

    Task DeleteFileFromDeviceAsync(string filePathOnDevice, string deviceRootPath);
}
