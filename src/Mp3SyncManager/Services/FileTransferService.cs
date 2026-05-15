using Mp3SyncManager.Models;
using Mp3SyncManager.Services.Interfaces;

namespace Mp3SyncManager.Services;

public class FileTransferService : IFileTransferService
{
    private readonly IDeviceDetectionService _deviceDetection;

    public FileTransferService(IDeviceDetectionService deviceDetection)
    {
        _deviceDetection = deviceDetection;
    }

    public IReadOnlyList<MusicFile> ListFiles(string folderPath, string searchPattern = "*.mp3", bool displayRelativePaths = false)
    {
        if (!Directory.Exists(folderPath))
            return [];

        var normalizedRoot = Path.GetFullPath(folderPath);
        var results = new List<MusicFile>();

        foreach (var path in EnumerateFilesSafe(normalizedRoot, searchPattern, displayRelativePaths))
        {
            try
            {
                var displayName = displayRelativePaths
                    ? Path.GetRelativePath(normalizedRoot, path)
                    : Path.GetFileName(path);

                results.Add(new MusicFile
                {
                    FileName = displayName,
                    FullPath = path,
                    FileSizeBytes = new FileInfo(path).Length,
                });
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        results.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase));
        return results.AsReadOnly();
    }

    /// <summary>
    /// Recursively enumerates files under <paramref name="root"/> matching
    /// <paramref name="searchPattern"/>, skipping any directories that are
    /// inaccessible due to <see cref="UnauthorizedAccessException"/> or
    /// <see cref="IOException"/>. When <paramref name="recurse"/> is false,
    /// only the top-level directory is enumerated.
    /// </summary>
    private static IEnumerable<string> EnumerateFilesSafe(string root, string searchPattern, bool recurse)
    {
        if (!recurse)
        {
            IEnumerable<string> topFiles = [];
            try { topFiles = Directory.EnumerateFiles(root, searchPattern); }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            foreach (var f in topFiles) yield return f;
            yield break;
        }

        var dirs = new Queue<string>();
        dirs.Enqueue(root);

        while (dirs.Count > 0)
        {
            var dir = dirs.Dequeue();

            IEnumerable<string> files = [];
            try { files = Directory.EnumerateFiles(dir, searchPattern); }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }
            foreach (var f in files) yield return f;

            IEnumerable<string> subdirs = [];
            try { subdirs = Directory.EnumerateDirectories(dir); }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }
            foreach (var d in subdirs) dirs.Enqueue(d);
        }
    }

    public async Task CopyFileAsync(
        string sourceFilePath,
        string sourceFolderPath,
        string deviceRootPath,
        IProgress<TransferProgress>? progress,
        bool overwriteExisting,
        CancellationToken cancellationToken)
    {
        AssertWithinSourceRoot(sourceFilePath, sourceFolderPath);

        var isActiveDevice = _deviceDetection.GetCurrentDevices()
            .Any(d => string.Equals(
                d.RootPath.TrimEnd(Path.DirectorySeparatorChar),
                deviceRootPath.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase));

        if (!isActiveDevice)
            throw new DeviceNotAvailableException(deviceRootPath);

        // Destination mirrors the source folder structure so that files from different
        // subdirectories (e.g. Artist1\song.mp3 and Artist2\song.mp3) never collide.
        // The relative path is computed from sourceFolderPath so that
        // C:\Music\Artist1\song.mp3 -> E:\Artist1\song.mp3 on the device.
        var normalizedSourceRoot = Path.GetFullPath(sourceFolderPath);
        var normalizedSourceFile = Path.GetFullPath(sourceFilePath);
        var relativeSubPath = Path.GetRelativePath(normalizedSourceRoot, normalizedSourceFile);
        var destPath = Path.Combine(deviceRootPath, relativeSubPath);

        // H-2: assert the computed destination stays within the device root
        // (guards against a relative sub-path that escapes via ".." segments).
        var normalizedDeviceRoot = Path.GetFullPath(deviceRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedDest = Path.GetFullPath(destPath);
        if (!normalizedDest.StartsWith(normalizedDeviceRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Copy destination '{normalizedDest}' would be outside device root '{normalizedDeviceRoot}'.");

        // H-3: give callers a plain-language typed exception instead of a raw IOException
        // from FileMode.CreateNew when the file already exists.
        if (!overwriteExisting && File.Exists(destPath))
            throw new FileAlreadyExistsOnDeviceException(Path.GetFileName(destPath));

        // Create any intermediate subdirectories on the device if they do not yet exist.
        var destDir = Path.GetDirectoryName(destPath);
        if (destDir is not null && !Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        var fileName = Path.GetFileName(sourceFilePath);
        var totalBytes = new FileInfo(sourceFilePath).Length;

        const int bufferSize = 81920;
        await using var source = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        await using var dest = new FileStream(destPath, overwriteExisting ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);

        var buffer = new byte[bufferSize];
        long bytesTransferred = 0;
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            bytesTransferred += bytesRead;
            progress?.Report(new TransferProgress
            {
                FileName = fileName,
                BytesTransferred = bytesTransferred,
                TotalBytes = totalBytes,
            });
        }

        // Always report 100% completion regardless of buffer alignment.
        progress?.Report(new TransferProgress
        {
            FileName = fileName,
            BytesTransferred = totalBytes,
            TotalBytes = totalBytes,
        });
    }

    public Task DeleteFileFromDeviceAsync(string filePathOnDevice, string deviceRootPath)
    {
        AssertWithinDeviceRoot(filePathOnDevice, deviceRootPath);

        if (!File.Exists(filePathOnDevice))
            throw new FileNotFoundException("File not found on device.", filePathOnDevice);

        File.Delete(filePathOnDevice);
        return Task.CompletedTask;
    }

    private static void AssertWithinSourceRoot(string sourceFilePath, string sourceFolderPath)
    {
        var resolvedFile = Path.GetFullPath(sourceFilePath);
        var resolvedRoot = Path.GetFullPath(sourceFolderPath).TrimEnd(Path.DirectorySeparatorChar)
                           + Path.DirectorySeparatorChar;

        if (!resolvedFile.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Copy source '{resolvedFile}' is outside the configured source folder '{resolvedRoot}'.");
    }

    private void AssertWithinDeviceRoot(string filePathOnDevice, string deviceRootPath)
    {
        var resolvedFile = Path.GetFullPath(filePathOnDevice);
        var resolvedRoot = Path.GetFullPath(deviceRootPath).TrimEnd(Path.DirectorySeparatorChar)
                           + Path.DirectorySeparatorChar;

        // Guard 1: prefix + trailing separator prevents ../traversal attacks
        if (!resolvedFile.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Delete target '{resolvedFile}' is outside device root '{resolvedRoot}'.");

        // Guard 2: same drive root — redundant safety net for junction points / UNC paths
        if (!string.Equals(
                Path.GetPathRoot(resolvedFile),
                Path.GetPathRoot(resolvedRoot),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Delete target '{resolvedFile}' is on a different drive than device root '{resolvedRoot}'.");

        // Guard 3: confirm the device is still actively mounted as a removable drive
        var isActiveDevice = _deviceDetection.GetCurrentDevices()
            .Any(d => string.Equals(
                d.RootPath.TrimEnd(Path.DirectorySeparatorChar),
                deviceRootPath.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase));

        if (!isActiveDevice)
            throw new InvalidOperationException(
                $"Device at '{deviceRootPath}' is no longer detected as a connected removable drive.");
    }
}
