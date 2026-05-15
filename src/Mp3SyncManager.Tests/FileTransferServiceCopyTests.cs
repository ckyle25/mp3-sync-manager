using Mp3SyncManager.Models;
using Mp3SyncManager.Services;
using Mp3SyncManager.Services.Interfaces;
using NSubstitute;

namespace Mp3SyncManager.Tests;

public class FileTransferServiceCopyTests : IDisposable
{
    private readonly string _sourceRoot;
    private readonly string _deviceRoot;
    private readonly IDeviceDetectionService _deviceDetection;
    private readonly FileTransferService _sut;

    public FileTransferServiceCopyTests()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), "Mp3SyncManagerCopyTests", Guid.NewGuid().ToString());
        _sourceRoot = Path.Combine(tempBase, "Source");
        _deviceRoot = Path.Combine(tempBase, "Device");
        Directory.CreateDirectory(_sourceRoot);
        Directory.CreateDirectory(_deviceRoot);

        _deviceDetection = Substitute.For<IDeviceDetectionService>();
        var device = new DetectedDevice { RootPath = _deviceRoot };
        _deviceDetection.GetCurrentDevices().Returns(new List<DetectedDevice> { device }.AsReadOnly());

        _sut = new FileTransferService(_deviceDetection);
    }

    public void Dispose()
    {
        var tempBase = Path.GetDirectoryName(_sourceRoot)!;
        if (Directory.Exists(tempBase))
            Directory.Delete(tempBase, recursive: true);
    }

    [Fact]
    public async Task CopyFile_FileInSubfolder_DestinationMirrorsSubfolderStructure()
    {
        // Arrange: source file is at Artist\song.mp3 inside the source root
        var artistDir = Path.Combine(_sourceRoot, "Artist");
        Directory.CreateDirectory(artistDir);
        var sourceFile = Path.Combine(artistDir, "song.mp3");
        await File.WriteAllTextAsync(sourceFile, "fake mp3 data");

        // Act
        await _sut.CopyFileAsync(
            sourceFile,
            _sourceRoot,
            _deviceRoot,
            progress: null,
            overwriteExisting: false,
            cancellationToken: CancellationToken.None);

        // Assert: destination preserves the Artist subdirectory
        var expectedDest = Path.Combine(_deviceRoot, "Artist", "song.mp3");
        Assert.True(File.Exists(expectedDest), $"Expected file at '{expectedDest}'");
    }

    [Fact]
    public async Task CopyFile_TwoFilesWithSameBaseNameInDifferentSubfolders_BothCopiedWithoutCollision()
    {
        // Arrange: Artist1\track.mp3 and Artist2\track.mp3 share the same base name
        var dir1 = Path.Combine(_sourceRoot, "Artist1");
        var dir2 = Path.Combine(_sourceRoot, "Artist2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        var file1 = Path.Combine(dir1, "track.mp3");
        var file2 = Path.Combine(dir2, "track.mp3");
        await File.WriteAllTextAsync(file1, "artist1 data");
        await File.WriteAllTextAsync(file2, "artist2 data");

        // Act: copy both files in sequence
        await _sut.CopyFileAsync(file1, _sourceRoot, _deviceRoot, null, overwriteExisting: false, CancellationToken.None);
        await _sut.CopyFileAsync(file2, _sourceRoot, _deviceRoot, null, overwriteExisting: false, CancellationToken.None);

        // Assert: both exist at distinct paths on the device
        var destFile1 = Path.Combine(_deviceRoot, "Artist1", "track.mp3");
        var destFile2 = Path.Combine(_deviceRoot, "Artist2", "track.mp3");
        Assert.True(File.Exists(destFile1), $"Expected file at '{destFile1}'");
        Assert.True(File.Exists(destFile2), $"Expected file at '{destFile2}'");
        Assert.NotEqual(destFile1, destFile2);
    }

    [Fact]
    public async Task CopyFile_FileAtSourceRoot_DestinationIsDirectlyInDeviceRoot()
    {
        // Arrange: file lives directly in the source root, no subdirectory
        var sourceFile = Path.Combine(_sourceRoot, "song.mp3");
        await File.WriteAllTextAsync(sourceFile, "fake mp3 data");

        // Act
        await _sut.CopyFileAsync(
            sourceFile,
            _sourceRoot,
            _deviceRoot,
            progress: null,
            overwriteExisting: false,
            cancellationToken: CancellationToken.None);

        // Assert: destination is deviceRoot\song.mp3 with no extra subdirectory
        var expectedDest = Path.Combine(_deviceRoot, "song.mp3");
        Assert.True(File.Exists(expectedDest), $"Expected file at '{expectedDest}'");

        // No extra subdirectories should have been created inside _deviceRoot
        var subDirs = Directory.GetDirectories(_deviceRoot);
        Assert.Empty(subDirs);
    }

    [Fact]
    public async Task CopyFile_DestinationStaysWithinDeviceRoot()
    {
        // Arrange: source file is in a nested subdirectory
        var artistDir = Path.Combine(_sourceRoot, "Artist", "Album");
        Directory.CreateDirectory(artistDir);
        var sourceFile = Path.Combine(artistDir, "track.mp3");
        await File.WriteAllTextAsync(sourceFile, "nested file data");

        // Act
        await _sut.CopyFileAsync(
            sourceFile,
            _sourceRoot,
            _deviceRoot,
            progress: null,
            overwriteExisting: false,
            cancellationToken: CancellationToken.None);

        // Assert: the destination is inside the device root at the mirrored nested path
        var expectedDest = Path.Combine(_deviceRoot, "Artist", "Album", "track.mp3");
        Assert.True(File.Exists(expectedDest), $"Expected file at '{expectedDest}'");

        // Verify the full path actually starts with the device root (H-2 guard passes)
        var normalizedDeviceRoot = Path.GetFullPath(_deviceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Assert.True(
            Path.GetFullPath(expectedDest).StartsWith(normalizedDeviceRoot, StringComparison.OrdinalIgnoreCase),
            $"Expected '{expectedDest}' to be within device root '{normalizedDeviceRoot}'");
    }

    [Fact]
    public async Task CopyFile_DestinationEscapeAttemptViaRelativePath_Throws()
    {
        // Arrange: source file is outside the declared source root.
        // AssertWithinSourceRoot fires because the file path does not start with sourceRoot.
        var outsideFile = Path.Combine(Path.GetTempPath(), $"escape_{Guid.NewGuid()}.mp3");
        await File.WriteAllTextAsync(outsideFile, "should not copy");

        try
        {
            // Act & Assert: InvalidOperationException from AssertWithinSourceRoot
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.CopyFileAsync(
                    outsideFile,
                    _sourceRoot,
                    _deviceRoot,
                    progress: null,
                    overwriteExisting: false,
                    cancellationToken: CancellationToken.None));
        }
        finally
        {
            if (File.Exists(outsideFile)) File.Delete(outsideFile);
        }
    }

    [Fact]
    public async Task CopyFile_OverwriteExistingFalse_FileAlreadyExists_ThrowsInvalidOperation()
    {
        // Arrange: copy the file once successfully
        var sourceFile = Path.Combine(_sourceRoot, "song.mp3");
        var originalContent = "original content";
        await File.WriteAllTextAsync(sourceFile, originalContent);

        await _sut.CopyFileAsync(
            sourceFile, _sourceRoot, _deviceRoot,
            progress: null, overwriteExisting: false,
            cancellationToken: CancellationToken.None);

        var destPath = Path.Combine(_deviceRoot, "song.mp3");
        Assert.True(File.Exists(destPath));

        // Mutate the source to confirm the original device file is unchanged after the throw
        await File.WriteAllTextAsync(sourceFile, "new content that must not overwrite");

        // Act & Assert: second copy with overwriteExisting: false throws FileAlreadyExistsOnDeviceException (H-3)
        await Assert.ThrowsAsync<FileAlreadyExistsOnDeviceException>(() =>
            _sut.CopyFileAsync(
                sourceFile, _sourceRoot, _deviceRoot,
                progress: null, overwriteExisting: false,
                cancellationToken: CancellationToken.None));

        // Assert: original file content on device is unchanged
        var deviceContent = await File.ReadAllTextAsync(destPath);
        Assert.Equal(originalContent, deviceContent);
    }

    [Fact]
    public async Task CopyFile_DeviceNoLongerDetected_ThrowsDeviceNotAvailableException()
    {
        // Arrange: device is no longer present
        _deviceDetection.GetCurrentDevices().Returns(new List<DetectedDevice>().AsReadOnly());

        var sourceFile = Path.Combine(_sourceRoot, "song.mp3");
        await File.WriteAllTextAsync(sourceFile, "fake mp3 data");

        // Act & Assert: DeviceNotAvailableException because device is not detected
        await Assert.ThrowsAsync<DeviceNotAvailableException>(() =>
            _sut.CopyFileAsync(
                sourceFile,
                _sourceRoot,
                _deviceRoot,
                progress: null,
                overwriteExisting: false,
                cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task CopyFile_FileAlreadyExists_OverwriteFalse_ThrowsFileAlreadyExistsOnDeviceException()
    {
        // Arrange: copy file once to get it onto the device
        var sourceFile = Path.Combine(_sourceRoot, "song.mp3");
        await File.WriteAllTextAsync(sourceFile, "original content");

        await _sut.CopyFileAsync(
            sourceFile, _sourceRoot, _deviceRoot,
            progress: null, overwriteExisting: false,
            cancellationToken: CancellationToken.None);

        // Act & Assert: second copy throws the typed exception specifically
        await Assert.ThrowsAsync<FileAlreadyExistsOnDeviceException>(() =>
            _sut.CopyFileAsync(
                sourceFile, _sourceRoot, _deviceRoot,
                progress: null, overwriteExisting: false,
                cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task CopyFile_OverwriteExistingTrue_ReplacesFileContent()
    {
        // Arrange: write file A to device
        var sourceFile = Path.Combine(_sourceRoot, "song.mp3");
        await File.WriteAllTextAsync(sourceFile, "content A");

        await _sut.CopyFileAsync(
            sourceFile, _sourceRoot, _deviceRoot,
            progress: null, overwriteExisting: false,
            cancellationToken: CancellationToken.None);

        // Update source to content B
        await File.WriteAllTextAsync(sourceFile, "content B");

        // Act: copy with overwrite enabled
        await _sut.CopyFileAsync(
            sourceFile, _sourceRoot, _deviceRoot,
            progress: null, overwriteExisting: true,
            cancellationToken: CancellationToken.None);

        // Assert: device file now contains content B
        var destPath = Path.Combine(_deviceRoot, "song.mp3");
        var deviceContent = await File.ReadAllTextAsync(destPath);
        Assert.Equal("content B", deviceContent);
    }

    [Fact]
    public async Task CopyFile_Progress_ReachesCompletion()
    {
        // Arrange: small file
        var sourceFile = Path.Combine(_sourceRoot, "progress_test.mp3");
        await File.WriteAllTextAsync(sourceFile, "small file data for progress test");

        var reports = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(r => reports.Add(r));

        // Act
        await _sut.CopyFileAsync(
            sourceFile, _sourceRoot, _deviceRoot,
            progress, overwriteExisting: false,
            cancellationToken: CancellationToken.None);

        // Progress events are raised on the thread pool; give them a moment to arrive.
        await Task.Delay(50);

        // Assert: at least one report received
        Assert.NotEmpty(reports);

        // Assert: the final report shows BytesTransferred == TotalBytes (100% completion)
        var last = reports[^1];
        Assert.Equal(last.TotalBytes, last.BytesTransferred);
    }

    [Fact]
    public async Task CopyFile_FileContentIsCorrect_RootLevel()
    {
        // Arrange: source file with known binary-like content
        var sourceFile = Path.Combine(_sourceRoot, "content_check.mp3");
        var expectedBytes = new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00, 0x00, 0xFF, 0xAB };
        await File.WriteAllBytesAsync(sourceFile, expectedBytes);

        // Act
        await _sut.CopyFileAsync(
            sourceFile, _sourceRoot, _deviceRoot,
            progress: null, overwriteExisting: false,
            cancellationToken: CancellationToken.None);

        // Assert: device file bytes match source exactly
        var destPath = Path.Combine(_deviceRoot, "content_check.mp3");
        var actualBytes = await File.ReadAllBytesAsync(destPath);
        Assert.Equal(expectedBytes, actualBytes);
    }

    [Fact]
    public async Task CopyFile_FileContentIsCorrect_Subfolder()
    {
        // Arrange: source file inside a subdirectory
        var subDir = Path.Combine(_sourceRoot, "Artist", "Album");
        Directory.CreateDirectory(subDir);
        var sourceFile = Path.Combine(subDir, "nested_content.mp3");
        var expectedBytes = new byte[] { 0x49, 0x44, 0x33, 0x03, 0x00, 0x00, 0xCC, 0xDD };
        await File.WriteAllBytesAsync(sourceFile, expectedBytes);

        // Act
        await _sut.CopyFileAsync(
            sourceFile, _sourceRoot, _deviceRoot,
            progress: null, overwriteExisting: false,
            cancellationToken: CancellationToken.None);

        // Assert: device file bytes match source exactly
        var destPath = Path.Combine(_deviceRoot, "Artist", "Album", "nested_content.mp3");
        var actualBytes = await File.ReadAllBytesAsync(destPath);
        Assert.Equal(expectedBytes, actualBytes);
    }
}
