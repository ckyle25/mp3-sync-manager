using Mp3SyncManager.Models;
using Mp3SyncManager.Services;
using Mp3SyncManager.Services.Interfaces;
using NSubstitute;

namespace Mp3SyncManager.Tests;

public class FileTransferServiceDeleteTests : IDisposable
{
    private readonly string _testRoot;
    private readonly IDeviceDetectionService _deviceDetection;
    private readonly FileTransferService _sut;

    public FileTransferServiceDeleteTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Mp3SyncManagerTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);

        _deviceDetection = Substitute.For<IDeviceDetectionService>();
        _sut = new FileTransferService(_deviceDetection);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    private void MockDeviceReturnsRoot()
    {
        var device = new DetectedDevice { RootPath = _testRoot };
        _deviceDetection.GetCurrentDevices().Returns(new List<DetectedDevice> { device }.AsReadOnly());
    }

    [Fact]
    public async Task Delete_FileWithinDeviceRoot_DeletesFile()
    {
        // Arrange
        MockDeviceReturnsRoot();
        var filePath = Path.Combine(_testRoot, "song.mp3");
        await File.WriteAllTextAsync(filePath, "fake mp3 data");

        // Act
        await _sut.DeleteFileFromDeviceAsync(filePath, _testRoot);

        // Assert
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task Delete_FileOutsideDeviceRoot_Throws_InvalidOperation()
    {
        // Arrange — file path escapes via ..
        // Use _testRoot as device root, but file path walks up to a sibling directory
        var escapeTarget = Path.Combine(_testRoot, "..", "other", "file.mp3");
        MockDeviceReturnsRoot();

        // Act & Assert — Guard 1
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteFileFromDeviceAsync(escapeTarget, _testRoot));
    }

    [Fact]
    public async Task Delete_TraversalAttempt_Throws_InvalidOperation()
    {
        // Arrange — path has multiple traversal segments that resolve outside root
        var traversalPath = Path.Combine(_testRoot, "subdir", "..", "..", "outsideFile.mp3");
        MockDeviceReturnsRoot();

        // Act & Assert — Guard 1
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteFileFromDeviceAsync(traversalPath, _testRoot));
    }

    [Fact]
    public async Task Delete_DeviceNoLongerDetected_Throws_InvalidOperation()
    {
        // Arrange — file is within root, but device list is empty (Guard 3)
        var filePath = Path.Combine(_testRoot, "song.mp3");
        await File.WriteAllTextAsync(filePath, "fake mp3 data");
        _deviceDetection.GetCurrentDevices().Returns(new List<DetectedDevice>().AsReadOnly());

        // Act & Assert — Guard 3
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteFileFromDeviceAsync(filePath, _testRoot));
    }

    [Fact]
    public async Task Delete_FileDoesNotExist_Throws_FileNotFound()
    {
        // Arrange — device is detected and path is valid, but the file was never created
        MockDeviceReturnsRoot();
        var missingFile = Path.Combine(_testRoot, "ghost.mp3");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _sut.DeleteFileFromDeviceAsync(missingFile, _testRoot));
    }

    [Fact]
    public async Task Delete_SourceFolderAsDeviceRoot_Throws_InvalidOperation()
    {
        // Arrange
        // Only _testRoot is registered as a known device — not sourceFolderRoot
        MockDeviceReturnsRoot();

        var sourceFolderRoot = Path.Combine(Path.GetTempPath(), "Mp3SyncManagerTests_Source", Guid.NewGuid().ToString());
        Directory.CreateDirectory(sourceFolderRoot);

        try
        {
            var fileInSourceFolder = Path.Combine(sourceFolderRoot, "song.mp3");
            await File.WriteAllTextAsync(fileInSourceFolder, "fake mp3 data");

            // Guard 1 passes: file is within sourceFolderRoot
            // Guard 2 passes: same drive (both under %TEMP%)
            // Guard 3 fails: sourceFolderRoot is not in the detected devices list

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.DeleteFileFromDeviceAsync(fileInSourceFolder, sourceFolderRoot));
        }
        finally
        {
            if (Directory.Exists(sourceFolderRoot))
                Directory.Delete(sourceFolderRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Delete_FileInNestedDeviceSubdir_Succeeds()
    {
        // Arrange: file at deviceRoot\Artist\song.mp3 (mirrors the copy structure)
        MockDeviceReturnsRoot();
        var artistDir = Path.Combine(_testRoot, "Artist");
        Directory.CreateDirectory(artistDir);
        var filePath = Path.Combine(artistDir, "song.mp3");
        await File.WriteAllTextAsync(filePath, "fake mp3 data");

        // Act
        await _sut.DeleteFileFromDeviceAsync(filePath, _testRoot);

        // Assert: file is gone; the guard accepted the nested path
        Assert.False(File.Exists(filePath));
    }

    // Guard 2 requires two physical drives — tested manually

    [Fact]
    public async Task CopyFile_SourceOutsideSourceFolder_Throws_InvalidOperation()
    {
        var sourceFolder = Path.Combine(Path.GetTempPath(), "Mp3SyncManagerTests_CopySrc", Guid.NewGuid().ToString());
        Directory.CreateDirectory(sourceFolder);

        try
        {
            var outsideFile = Path.Combine(Path.GetTempPath(), $"outside_{Guid.NewGuid()}.mp3");
            await File.WriteAllTextAsync(outsideFile, "fake mp3 data");

            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _sut.CopyFileAsync(
                        outsideFile,
                        sourceFolder,
                        _testRoot,
                        progress: null,
                        overwriteExisting: false,
                        cancellationToken: CancellationToken.None));
            }
            finally
            {
                if (File.Exists(outsideFile)) File.Delete(outsideFile);
            }
        }
        finally
        {
            if (Directory.Exists(sourceFolder))
                Directory.Delete(sourceFolder, recursive: true);
        }
    }

    [Fact]
    public async Task CopyFile_SourceWithinSourceFolder_CopiesFile()
    {
        MockDeviceReturnsRoot();
        var sourceFolder = Path.Combine(Path.GetTempPath(), "Mp3SyncManagerTests_CopySrc", Guid.NewGuid().ToString());
        Directory.CreateDirectory(sourceFolder);

        try
        {
            var sourceFile = Path.Combine(sourceFolder, "song.mp3");
            await File.WriteAllTextAsync(sourceFile, "fake mp3 data");

            await _sut.CopyFileAsync(
                sourceFile,
                sourceFolder,
                _testRoot,
                progress: null,
                overwriteExisting: false,
                cancellationToken: CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(_testRoot, "song.mp3")));
        }
        finally
        {
            if (Directory.Exists(sourceFolder))
                Directory.Delete(sourceFolder, recursive: true);
        }
    }

    [Fact]
    public async Task CopyFile_DeviceRootNotInDetectedDevices_Throws_InvalidOperation()
    {
        // Arrange — device list is empty (Guard 3 equivalent for copy)
        _deviceDetection.GetCurrentDevices().Returns(new List<DetectedDevice>().AsReadOnly());

        var sourceFolder = Path.Combine(Path.GetTempPath(), "Mp3SyncManagerTests_CopySrc", Guid.NewGuid().ToString());
        Directory.CreateDirectory(sourceFolder);

        try
        {
            var sourceFile = Path.Combine(sourceFolder, "song.mp3");
            await File.WriteAllTextAsync(sourceFile, "fake mp3 data");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.CopyFileAsync(
                    sourceFile,
                    sourceFolder,
                    _testRoot,
                    progress: null,
                    overwriteExisting: false,
                    cancellationToken: CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(sourceFolder))
                Directory.Delete(sourceFolder, recursive: true);
        }
    }
}
