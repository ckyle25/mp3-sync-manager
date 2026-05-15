using System.Collections.ObjectModel;
using Mp3SyncManager.Models;
using Mp3SyncManager.Services.Interfaces;
using Mp3SyncManager.ViewModels;
using NSubstitute;

namespace Mp3SyncManager.Tests;

public class DeviceViewModelTests
{
    private readonly IFileTransferService _fileTransfer;
    private readonly DeviceViewModel _sut;

    public DeviceViewModelTests()
    {
        _fileTransfer = Substitute.For<IFileTransferService>();
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(new List<MusicFile>().AsReadOnly());
        _sut = new DeviceViewModel(_fileTransfer);
    }

    [Fact]
    public void OnActiveDeviceChanged_Null_ClearsFilesAndIsLoadingFalse()
    {
        // Put the vm into an active state first
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile>
            {
                new() { FileName = "a.mp3", FullPath = @"C:\a.mp3", FileSizeBytes = 1 },
            }.AsReadOnly());

        _sut.ActiveDevice = new DetectedDevice { RootPath = Path.GetTempPath() };

        // Now clear the device
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile>().AsReadOnly());
        _sut.ActiveDevice = null;

        Assert.Empty(_sut.Files);
        Assert.False(_sut.IsLoading);
    }

    [Fact]
    public void OnActiveDeviceChanged_DeviceSet_TriggersFileLoad()
    {
        var fakeFiles = new List<MusicFile>
        {
            new() { FileName = "a.mp3", FullPath = @"C:\a.mp3", FileSizeBytes = 1 },
            new() { FileName = "b.mp3", FullPath = @"C:\b.mp3", FileSizeBytes = 2 },
        }.AsReadOnly();

        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(fakeFiles);

        _sut.ActiveDevice = new DetectedDevice { RootPath = Path.GetTempPath() };

        Assert.Equal(2, _sut.Files.Count);
        Assert.False(_sut.IsLoading);
    }

    [Fact]
    public void SnapshotGuard_FilesNotAssigned_WhenDeviceClearedDuringRefresh()
    {
        // Arrange: when ListFiles is called, the side-effect clears ActiveDevice before returning files.
        // This simulates a disconnect during the synchronous scan.
        var fakeFiles = new List<MusicFile>
        {
            new() { FileName = "a.mp3", FullPath = @"C:\a.mp3", FileSizeBytes = 1 },
            new() { FileName = "b.mp3", FullPath = @"C:\b.mp3", FileSizeBytes = 2 },
        }.AsReadOnly();

        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(_ =>
            {
                // Simulate disconnect: clear the active device mid-call
                _sut.ActiveDevice = null;
                return fakeFiles;
            });

        // Act
        _sut.ActiveDevice = new DetectedDevice { RootPath = Path.GetTempPath() };

        // Assert: ListFiles was actually called (side effect fired)
        _fileTransfer.Received(1).ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());

        // Assert: the snapshot guard prevented the stale file list from being assigned
        Assert.Empty(_sut.Files);
    }

    [Fact]
    public void HasFiles_IsFalse_WhenNoDevice()
    {
        // Default state: no device, no files
        Assert.False(_sut.HasFiles);
    }

    [Fact]
    public void ShowEmptyDeviceMessage_IsTrue_WhenDeviceConnectedNoFiles()
    {
        // ListFiles already returns empty by default from constructor setup
        _sut.ActiveDevice = new DetectedDevice { RootPath = Path.GetTempPath() };

        Assert.True(_sut.ShowEmptyDeviceMessage);
    }

    [Fact]
    public void IsLoading_IsFalseAfterSync_WhenDeviceSet()
    {
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile>().AsReadOnly());

        _sut.ActiveDevice = new DetectedDevice { RootPath = Path.GetTempPath() };

        Assert.False(_sut.IsLoading);
        Assert.True(_sut.ShowEmptyDeviceMessage);
    }

    [Fact]
    public void HasFiles_ReflectsLatestCollection_AfterMultipleFilesReassignments()
    {
        var device = new DetectedDevice { RootPath = Path.GetTempPath() };

        // First assignment: 1 file
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile>
            {
                new() { FileName = "a.mp3", FullPath = @"C:\a.mp3", FileSizeBytes = 1 },
            }.AsReadOnly());
        _sut.ActiveDevice = device;
        Assert.True(_sut.HasFiles);

        // Second assignment: 0 files (new device set triggers new collection)
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile>().AsReadOnly());
        _sut.ActiveDevice = null;
        _sut.ActiveDevice = device;
        Assert.False(_sut.HasFiles);

        // Third assignment: 2 files
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile>
            {
                new() { FileName = "a.mp3", FullPath = @"C:\a.mp3", FileSizeBytes = 1 },
                new() { FileName = "b.mp3", FullPath = @"C:\b.mp3", FileSizeBytes = 2 },
            }.AsReadOnly());
        _sut.ActiveDevice = null;
        _sut.ActiveDevice = device;
        Assert.True(_sut.HasFiles);
        Assert.Equal(2, _sut.Files.Count);
    }

    [Fact]
    public void DeviceStorageText_IsNull_WhenNoDevice()
    {
        Assert.Null(_sut.DeviceStorageText);
    }

    [Fact]
    public void DeviceStorageText_IsNull_WhenTotalSizeBytesIsZero()
    {
        _sut.ActiveDevice = new DetectedDevice { RootPath = "E:\\", TotalSizeBytes = 0 };
        Assert.Null(_sut.DeviceStorageText);
    }

    [Fact]
    public void DeviceStorageText_FormatsGigabyteDevice()
    {
        _sut.ActiveDevice = new DetectedDevice
        {
            RootPath = "E:\\",
            TotalSizeBytes = 2_147_483_648L,      // 2.0 GB
            AvailableFreeSpaceBytes = 1_073_741_824L, // 1.0 GB
        };
        Assert.Equal("1.0 GB free of 2.0 GB", _sut.DeviceStorageText);
    }

    [Fact]
    public void DeviceStorageText_FormatsMixedUnits()
    {
        _sut.ActiveDevice = new DetectedDevice
        {
            RootPath = "E:\\",
            TotalSizeBytes = 2_147_483_648L,      // 2.0 GB
            AvailableFreeSpaceBytes = 524_288_000L,  // 500 MB
        };
        Assert.Equal("500 MB free of 2.0 GB", _sut.DeviceStorageText);
    }

    [Fact]
    public async Task DeleteSelectedAsync_WhenDeleteFails_StatusMessageIsPlainLanguage()
    {
        _sut.ActiveDevice = new DetectedDevice { RootPath = "E:\\" };
        _sut.SelectedFile = new MusicFile { FileName = "a.mp3", FullPath = @"E:\a.mp3", FileSizeBytes = 1000 };

        _fileTransfer.DeleteFileFromDeviceAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns<Task>(_ => throw new InvalidOperationException(
                "Delete target 'E:\\a.mp3' is outside device root 'E:\\'."));

        await _sut.DeleteSelectedAsync();

        Assert.NotNull(_sut.StatusMessage);
        // Must not contain file paths or raw exception details
        Assert.DoesNotContain("E:\\", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Delete target", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteSelectedAsync_WhenDeleteSucceeds_ClearsStatusMessage()
    {
        _sut.ActiveDevice = new DetectedDevice { RootPath = "E:\\" };
        var file = new MusicFile { FileName = "a.mp3", FullPath = @"E:\a.mp3", FileSizeBytes = 1000 };
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile> { file }.AsReadOnly());
        _sut.ActiveDevice = null;
        _sut.ActiveDevice = _sut.ActiveDevice ?? new DetectedDevice { RootPath = "E:\\" };

        // Manually set state to test success path
        _sut.ActiveDevice = new DetectedDevice { RootPath = "E:\\" };
        _sut.SelectedFile = file;
        _sut.StatusMessage = "Previous error";

        _fileTransfer.DeleteFileFromDeviceAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        await _sut.DeleteSelectedAsync();

        Assert.Null(_sut.StatusMessage);
    }

    [Fact]
    public void DeviceViewModel_ListFiles_CalledWithDisplayRelativePathsTrue()
    {
        // Regression guard: RefreshFromDevice must pass displayRelativePaths: true
        // so that nested device files show their relative paths (e.g. Artist\Album\song.mp3),
        // matching the source library display style and making copied files visible.
        _sut.ActiveDevice = new DetectedDevice { RootPath = Path.GetTempPath() };

        _fileTransfer.Received(1).ListFiles(Arg.Any<string>(), Arg.Any<string>(), true);
    }

    [Fact]
    public async Task DeleteSelectedAsync_WhenFileInList_RemovesFromList()
    {
        var file = new MusicFile { FileName = "a.mp3", FullPath = @"E:\a.mp3", FileSizeBytes = 1000 };
        _sut.ActiveDevice = new DetectedDevice { RootPath = "E:\\" };
        _sut.Files.Add(file);
        _sut.SelectedFile = file;

        _fileTransfer.DeleteFileFromDeviceAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        await _sut.DeleteSelectedAsync();

        Assert.Empty(_sut.Files);
    }

    [Fact]
    public async Task DeleteSelectedAsync_FileNotFoundOnDevice_SetsFileNotFoundMessage()
    {
        var file = new MusicFile { FileName = "a.mp3", FullPath = @"E:\a.mp3", FileSizeBytes = 1000 };
        _sut.ActiveDevice = new DetectedDevice { RootPath = "E:\\" };
        _sut.SelectedFile = file;

        _fileTransfer.DeleteFileFromDeviceAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns<Task>(_ => throw new FileNotFoundException("not found", @"E:\a.mp3"));

        await _sut.DeleteSelectedAsync();

        Assert.NotNull(_sut.StatusMessage);
        Assert.Contains("not found", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteSelectedAsync_NullSelectedFile_DoesNotCallService()
    {
        _sut.ActiveDevice = new DetectedDevice { RootPath = "E:\\" };
        _sut.SelectedFile = null;

        await _sut.DeleteSelectedAsync();

        await _fileTransfer.DidNotReceive().DeleteFileFromDeviceAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task DeleteSelectedAsync_NullActiveDevice_DoesNotCallService()
    {
        _sut.ActiveDevice = null;
        _sut.SelectedFile = new MusicFile { FileName = "a.mp3", FullPath = @"E:\a.mp3", FileSizeBytes = 1000 };

        await _sut.DeleteSelectedAsync();

        await _fileTransfer.DidNotReceive().DeleteFileFromDeviceAsync(Arg.Any<string>(), Arg.Any<string>());
    }
}
