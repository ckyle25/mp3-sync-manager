using System.Collections.ObjectModel;
using Mp3SyncManager.Models;
using Mp3SyncManager.Services.Interfaces;
using Mp3SyncManager.ViewModels;
using NSubstitute;

namespace Mp3SyncManager.Tests;

public class LibraryViewModelTests
{
    private readonly IFileTransferService _fileTransfer;
    private readonly LibraryViewModel _sut;

    public LibraryViewModelTests()
    {
        _fileTransfer = Substitute.For<IFileTransferService>();
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(new List<MusicFile>().AsReadOnly());
        _sut = new LibraryViewModel(_fileTransfer);
    }

    [Fact]
    public void SourceFolderMissing_IsFalse_WhenPathIsEmpty()
    {
        // Default state: SourceFolderPath is empty
        Assert.False(_sut.SourceFolderMissing);
    }

    [Fact]
    public void SourceFolderMissing_IsFalse_WhenPathExists()
    {
        _sut.SourceFolderPath = Path.GetTempPath();

        Assert.False(_sut.SourceFolderMissing);
    }

    [Fact]
    public void SourceFolderMissing_IsTrue_WhenPathDoesNotExist()
    {
        _sut.SourceFolderPath = @"C:\ThisPathDefinitelyDoesNotExist_XYZ_99999";

        Assert.True(_sut.SourceFolderMissing);
    }

    [Fact]
    public void HasFiles_IsFalse_WhenFilesEmpty()
    {
        // Default state: no files loaded
        Assert.False(_sut.HasFiles);
    }

    [Fact]
    public void HasFiles_IsTrue_AfterRefreshWithFiles()
    {
        var fakeFiles = new List<MusicFile>
        {
            new() { FileName = "a.mp3", FullPath = @"C:\tmp\a.mp3", FileSizeBytes = 1000 },
            new() { FileName = "b.mp3", FullPath = @"C:\tmp\b.mp3", FileSizeBytes = 2000 },
        }.AsReadOnly();

        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(fakeFiles);
        _sut.SourceFolderPath = Path.GetTempPath();

        Assert.True(_sut.HasFiles);
    }

    [Fact]
    public void SelectedFiles_WhenAssigned_HasSelectedFilesBecomesTrue()
    {
        var file = new MusicFile { FileName = "a.mp3", FullPath = @"C:\Music\a.mp3", FileSizeBytes = 1000 };
        _sut.SelectedFiles = new ObservableCollection<MusicFile> { file };

        Assert.True(_sut.HasSelectedFiles);
    }

    [Fact]
    public void SelectedFiles_Empty_HasSelectedFilesFalse()
    {
        // Default state: SelectedFiles is an empty collection
        Assert.False(_sut.HasSelectedFiles);
    }

    [Fact]
    public void SelectedFiles_CollectionChanged_UpdatesHasSelectedFiles()
    {
        // Start with empty SelectedFiles; mutate in-place via Add (simulates Avalonia ListBox behavior)
        var file = new MusicFile { FileName = "a.mp3", FullPath = @"C:\Music\a.mp3", FileSizeBytes = 1000 };

        _sut.SelectedFiles.Add(file);

        Assert.True(_sut.HasSelectedFiles);
    }

    [Fact]
    public void SelectedFiles_CollectionChanged_ToEmpty_UpdatesHasSelectedFiles()
    {
        // Start with one item, remove it in-place
        var file = new MusicFile { FileName = "a.mp3", FullPath = @"C:\Music\a.mp3", FileSizeBytes = 1000 };
        _sut.SelectedFiles.Add(file);
        Assert.True(_sut.HasSelectedFiles); // precondition

        _sut.SelectedFiles.Remove(file);

        Assert.False(_sut.HasSelectedFiles);
    }

    [Fact]
    public void SelectedFiles_InitialCollection_PropertyChangedRaised_WhenItemAdded()
    {
        // Construct a fresh instance (no property replacement) and subscribe PropertyChanged
        var sut = new LibraryViewModel(_fileTransfer);
        var raisedNames = new List<string?>();
        sut.PropertyChanged += (_, e) => raisedNames.Add(e.PropertyName);

        sut.SelectedFiles.Add(new MusicFile { FileName = "a.mp3", FullPath = @"C:\Music\a.mp3", FileSizeBytes = 1 });

        Assert.Contains("HasSelectedFiles", raisedNames);
    }

    [Fact]
    public void SelectedFiles_InitialCollection_PropertyChangedRaised_WhenItemRemoved()
    {
        // Add an item first (before subscribing), then subscribe and remove
        var sut = new LibraryViewModel(_fileTransfer);
        var file = new MusicFile { FileName = "a.mp3", FullPath = @"C:\Music\a.mp3", FileSizeBytes = 1 };
        sut.SelectedFiles.Add(file);

        var raisedNames = new List<string?>();
        sut.PropertyChanged += (_, e) => raisedNames.Add(e.PropertyName);

        sut.SelectedFiles.Remove(file);

        Assert.Contains("HasSelectedFiles", raisedNames);
    }
}
