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

    // --- Artist/Album/Song grouping tests ---

    private static MusicFile MakeSong(string sourceRoot, string artist, string album, string fileName, long sizeBytes = 1000) =>
        new()
        {
            FileName = fileName,
            FullPath = Path.Combine(sourceRoot, artist, album, fileName),
            FileSizeBytes = sizeBytes
        };

    [Fact]
    public void Refresh_GroupsFilesByArtistAndAlbum()
    {
        var root = Path.GetTempPath();
        var song1 = MakeSong(root, "Beatles", "Abbey Road", "Come Together.mp3");
        var song2 = MakeSong(root, "Beatles", "Abbey Road", "Something.mp3");
        var song3 = MakeSong(root, "Pink Floyd", "The Wall", "Another Brick.mp3");

        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile> { song1, song2, song3 }.AsReadOnly());

        _sut.SourceFolderPath = root;

        Assert.Equal(2, _sut.Artists.Count);
        Assert.Equal("Beatles", _sut.Artists[0].Name);
        Assert.Equal("Pink Floyd", _sut.Artists[1].Name);
        Assert.Single(_sut.Artists[0].Albums);
        Assert.Equal("Abbey Road", _sut.Artists[0].Albums[0].AlbumName);
        Assert.Equal(2, _sut.Artists[0].Albums[0].Songs.Count);
        Assert.Single(_sut.Artists[1].Albums);
        Assert.Equal("The Wall", _sut.Artists[1].Albums[0].AlbumName);
    }

    [Fact]
    public void HasFiles_IsTrue_WhenArtistsPresentAfterRefresh()
    {
        var root = Path.GetTempPath();
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile> { MakeSong(root, "A", "B", "track.mp3") }.AsReadOnly());

        _sut.SourceFolderPath = root;

        Assert.True(_sut.HasFiles);
    }

    [Fact]
    public void HasSelectedAlbum_IsFalse_WhenNoAlbumSelected()
    {
        Assert.False(_sut.HasSelectedAlbum);
    }

    [Fact]
    public void HasSelectedAlbum_IsTrue_WhenAlbumWithSongsSelected()
    {
        var root = Path.GetTempPath();
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile> { MakeSong(root, "A", "B", "a.mp3") }.AsReadOnly());
        _sut.SourceFolderPath = root;

        _sut.SelectedArtist = _sut.Artists[0];
        _sut.SelectedAlbum = _sut.Artists[0].Albums[0];

        Assert.True(_sut.HasSelectedAlbum);
    }

    [Fact]
    public void SelectedArtist_Change_ClearsSelectedAlbum()
    {
        var root = Path.GetTempPath();
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile> { MakeSong(root, "A", "B", "a.mp3") }.AsReadOnly());
        _sut.SourceFolderPath = root;

        _sut.SelectedArtist = _sut.Artists[0];
        _sut.SelectedAlbum = _sut.Artists[0].Albums[0];
        Assert.NotNull(_sut.SelectedAlbum);

        _sut.SelectedArtist = null;

        Assert.Null(_sut.SelectedAlbum);
    }

    [Fact]
    public void SelectedAlbum_Change_ClearsSelectedFiles()
    {
        var root = Path.GetTempPath();
        var song = MakeSong(root, "A", "B", "a.mp3");
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile> { song }.AsReadOnly());
        _sut.SourceFolderPath = root;

        _sut.SelectedArtist = _sut.Artists[0];
        _sut.SelectedAlbum = _sut.Artists[0].Albums[0];
        _sut.SelectedFiles.Add(song);
        Assert.True(_sut.HasSelectedFiles);

        _sut.SelectedAlbum = null;

        Assert.False(_sut.HasSelectedFiles);
    }

    [Fact]
    public void CurrentAlbums_ReturnsEmpty_WhenNoArtistSelected()
    {
        Assert.Empty(_sut.CurrentAlbums);
    }

    [Fact]
    public void CurrentAlbums_ReturnsAlbumsForSelectedArtist()
    {
        var root = Path.GetTempPath();
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile> { MakeSong(root, "A", "B", "a.mp3") }.AsReadOnly());
        _sut.SourceFolderPath = root;

        _sut.SelectedArtist = _sut.Artists[0];

        Assert.Single(_sut.CurrentAlbums);
        Assert.Equal("B", _sut.CurrentAlbums[0].AlbumName);
    }

    [Fact]
    public void CurrentSongs_ReturnsEmpty_WhenNoAlbumSelected()
    {
        Assert.Empty(_sut.CurrentSongs);
    }

    [Fact]
    public void CurrentSongs_ReturnsSongsForSelectedAlbum()
    {
        var root = Path.GetTempPath();
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile> { MakeSong(root, "A", "B", "a.mp3") }.AsReadOnly());
        _sut.SourceFolderPath = root;

        _sut.SelectedArtist = _sut.Artists[0];
        _sut.SelectedAlbum = _sut.Artists[0].Albums[0];

        Assert.Single(_sut.CurrentSongs);
        Assert.Equal("a.mp3", _sut.CurrentSongs[0].FileName);
    }

    [Fact]
    public void Refresh_ClearsArtistAndAlbumSelection()
    {
        var root = Path.GetTempPath();
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile> { MakeSong(root, "A", "B", "a.mp3") }.AsReadOnly());
        _sut.SourceFolderPath = root;

        _sut.SelectedArtist = _sut.Artists[0];
        _sut.SelectedAlbum = _sut.Artists[0].Albums[0];

        _sut.Refresh();

        Assert.Null(_sut.SelectedArtist);
        Assert.Null(_sut.SelectedAlbum);
    }

    [Fact]
    public void HasArtistSelection_IsFalse_WhenNoArtistSelected()
    {
        Assert.False(_sut.HasArtistSelection);
    }

    [Fact]
    public void HasArtistSelection_IsTrue_WhenArtistSelected()
    {
        var root = Path.GetTempPath();
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile> { MakeSong(root, "A", "B", "a.mp3") }.AsReadOnly());
        _sut.SourceFolderPath = root;

        _sut.SelectedArtist = _sut.Artists[0];

        Assert.True(_sut.HasArtistSelection);
    }

    [Fact]
    public void Artists_SortedAlphabetically_AfterRefresh()
    {
        var root = Path.GetTempPath();
        var songs = new List<MusicFile>
        {
            MakeSong(root, "Zeppelin", "IV", "z.mp3"),
            MakeSong(root, "ABBA", "Arrival", "a.mp3"),
        };
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(songs.AsReadOnly());

        _sut.SourceFolderPath = root;

        Assert.Equal("ABBA", _sut.Artists[0].Name);
        Assert.Equal("Zeppelin", _sut.Artists[1].Name);
    }

    [Fact]
    public void HasSelectedAlbum_PropertyChanged_RaisedWhenAlbumSet()
    {
        var root = Path.GetTempPath();
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile> { MakeSong(root, "A", "B", "a.mp3") }.AsReadOnly());
        _sut.SourceFolderPath = root;
        _sut.SelectedArtist = _sut.Artists[0];

        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.SelectedAlbum = _sut.Artists[0].Albums[0];

        Assert.Contains(nameof(_sut.HasSelectedAlbum), raised);
    }
}
