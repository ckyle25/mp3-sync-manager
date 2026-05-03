using Mp3SyncManager.Models;
using Mp3SyncManager.Services.Interfaces;
using Mp3SyncManager.ViewModels;
using NSubstitute;

namespace Mp3SyncManager.Tests;

public class SetupViewModelTests
{
    private readonly ISettingsService _settingsService;
    private readonly IFileTransferService _fileTransfer;
    private readonly SetupViewModel _sut;

    public SetupViewModelTests()
    {
        _settingsService = Substitute.For<ISettingsService>();
        _fileTransfer = Substitute.For<IFileTransferService>();
        _sut = new SetupViewModel(_settingsService, _fileTransfer);
    }

    [Fact]
    public void ValidateAndPreview_EmptyPath_SetsValidationMessage_ConfirmVisibleFalse()
    {
        // SelectedFolderPath is empty by default
        _sut.ValidateAndPreview();

        Assert.NotNull(_sut.ValidationMessage);
        Assert.False(_sut.ConfirmVisible);
    }

    [Fact]
    public void ValidateAndPreview_NonExistentPath_SetsValidationMessage()
    {
        _sut.SelectedFolderPath = @"C:\FakePathThatDoesNotExist_99999";

        _sut.ValidateAndPreview();

        Assert.NotNull(_sut.ValidationMessage);
        Assert.Contains("could not be opened", _sut.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndPreview_FolderWithNoMp3s_SetsValidationMessage()
    {
        _sut.SelectedFolderPath = Path.GetTempPath();
        _fileTransfer
            .ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile>().AsReadOnly());

        _sut.ValidateAndPreview();

        Assert.NotNull(_sut.ValidationMessage);
        Assert.Contains("No music files were found", _sut.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndPreview_FolderWithMp3s_SetsConfirmVisible()
    {
        _sut.SelectedFolderPath = Path.GetTempPath();
        var fakeFiles = new List<MusicFile>
        {
            new() { FileName = "a.mp3", FullPath = @"C:\tmp\a.mp3", FileSizeBytes = 1000 },
            new() { FileName = "b.mp3", FullPath = @"C:\tmp\b.mp3", FileSizeBytes = 2000 },
            new() { FileName = "c.mp3", FullPath = @"C:\tmp\c.mp3", FileSizeBytes = 3000 },
        };
        _fileTransfer
            .ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(fakeFiles.AsReadOnly());

        _sut.ValidateAndPreview();

        Assert.True(_sut.ConfirmVisible);
        Assert.Equal(3, _sut.Mp3FileCount);
    }

    [Fact]
    public async Task ConfirmAsync_WhenConfirmNotVisible_DoesNotSave()
    {
        // ConfirmVisible is false by default
        await _sut.ConfirmAsync();

        await _settingsService.DidNotReceive().SaveAsync(Arg.Any<AppSettings>());
    }

    [Fact]
    public async Task ConfirmAsync_WhenConfirmVisible_SavesAndRaisesEvent()
    {
        // Set up a valid state so ValidateAndPreview makes ConfirmVisible = true
        _sut.SelectedFolderPath = Path.GetTempPath();
        var fakeFiles = new List<MusicFile>
        {
            new() { FileName = "a.mp3", FullPath = @"C:\tmp\a.mp3", FileSizeBytes = 1000 },
        };
        _fileTransfer
            .ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(fakeFiles.AsReadOnly());
        _sut.ValidateAndPreview();

        AppSettings? capturedSettings = null;
        _sut.SetupCompleted += (_, s) => capturedSettings = s;

        await _sut.ConfirmAsync();

        await _settingsService.Received(1).SaveAsync(Arg.Any<AppSettings>());
        Assert.NotNull(capturedSettings);
        Assert.Equal(Path.GetTempPath(), capturedSettings!.SourceFolderPath);
    }

    [Fact]
    public void ValidateAndPreview_CallsListFilesWithDisplayRelativePathsTrue()
    {
        // Arrange: folder exists and contains files so ValidateAndPreview reaches the ListFiles call
        _sut.SelectedFolderPath = Path.GetTempPath();
        _fileTransfer
            .ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile>
            {
                new() { FileName = "a.mp3", FullPath = @"C:\tmp\a.mp3", FileSizeBytes = 1000 },
            }.AsReadOnly());

        // Act
        _sut.ValidateAndPreview();

        // Assert: setup counting uses recursive listing (displayRelativePaths: true)
        _fileTransfer.Received(1).ListFiles(Arg.Any<string>(), Arg.Any<string>(), true);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        // Put viewmodel into a non-default state
        _sut.SelectedFolderPath = @"C:\SomeFolder";
        _sut.ValidationMessage = "some message";
        _sut.ConfirmVisible = true;
        _sut.Mp3FileCount = 42;

        _sut.Reset();

        Assert.Equal(string.Empty, _sut.SelectedFolderPath);
        Assert.Null(_sut.ValidationMessage);
        Assert.False(_sut.ConfirmVisible);
        Assert.Equal(0, _sut.Mp3FileCount);
    }
}
