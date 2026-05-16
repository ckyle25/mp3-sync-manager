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

    // --- SourceFolderMissing ---

    [Fact]
    public void SourceFolderMissing_IsFalse_WhenPathIsEmpty()
    {
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

    // --- HasFiles ---

    [Fact]
    public void HasFiles_IsFalse_WhenFilesEmpty()
    {
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

    // --- Artist/Album/Song grouping ---

    [Fact]
    public void Refresh_FilesWithArtistAlbumStructure_GroupsArtistsAlphabetically()
    {
        // FileName uses the relative path returned by ListFiles(displayRelativePaths:true).
        // Use forward slash so the test works cross-platform.
        var fakeFiles = new List<MusicFile>
        {
            new() { FileName = "Beatles/Abbey Road/Come Together.mp3", FullPath = "/music/Beatles/Abbey Road/Come Together.mp3", FileSizeBytes = 1000 },
            new() { FileName = "Beatles/Abbey Road/Something.mp3", FullPath = "/music/Beatles/Abbey Road/Something.mp3", FileSizeBytes = 2000 },
            new() { FileName = "Beatles/Help/Help!.mp3", FullPath = "/music/Beatles/Help/Help!.mp3", FileSizeBytes = 3000 },
            new() { FileName = "Eagles/Hotel California/Hotel California.mp3", FullPath = "/music/Eagles/Hotel California/Hotel California.mp3", FileSizeBytes = 4000 },
        }.AsReadOnly();

        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(fakeFiles);
        _sut.SourceFolderPath = Path.GetTempPath();

        Assert.Equal(2, _sut.Artists.Count);
        Assert.Equal("Beatles", _sut.Artists[0].Name);
        Assert.Equal("Eagles", _sut.Artists[1].Name);
    }

    [Fact]
    public void Refresh_FilesWithArtistAlbumStructure_GroupsAlbumsUnderArtist()
    {
        var fakeFiles = new List<MusicFile>
        {
            new() { FileName = "Beatles/Abbey Road/Come Together.mp3", FullPath = "/music/Beatles/Abbey Road/Come Together.mp3", FileSizeBytes = 1000 },
            new() { FileName = "Beatles/Abbey Road/Something.mp3", FullPath = "/music/Beatles/Abbey Road/Something.mp3", FileSizeBytes = 2000 },
            new() { FileName = "Beatles/Help/Help!.mp3", FullPath = "/music/Beatles/Help/Help!.mp3", FileSizeBytes = 3000 },
        }.AsReadOnly();

        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(fakeFiles);
        _sut.SourceFolderPath = Path.GetTempPath();

        var beatles = _sut.Artists[0];
        Assert.Equal(2, beatles.Albums.Count);
        Assert.Equal("Abbey Road", beatles.Albums[0].Name);
        Assert.Equal("Help", beatles.Albums[1].Name);
        Assert.Equal(2, beatles.Albums[0].Songs.Count);
    }

    [Fact]
    public void Refresh_FilesWithArtistAlbumStructure_SongFileNameIsJustFilename()
    {
        var fakeFiles = new List<MusicFile>
        {
            new() { FileName = "Beatles/Abbey Road/Come Together.mp3", FullPath = "/music/Beatles/Abbey Road/Come Together.mp3", FileSizeBytes = 1000 },
        }.AsReadOnly();

        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(fakeFiles);
        _sut.SourceFolderPath = Path.GetTempPath();

        var song = _sut.Artists[0].Albums[0].Songs[0];
        Assert.Equal("Come Together.mp3", song.FileName);
        Assert.Equal("/music/Beatles/Abbey Road/Come Together.mp3", song.FullPath);
    }

    [Fact]
    public void Refresh_FilesDirectlyInSource_GroupedAsOther()
    {
        var fakeFiles = new List<MusicFile>
        {
            new() { FileName = "track.mp3", FullPath = "/music/track.mp3", FileSizeBytes = 1000 },
        }.AsReadOnly();

        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(fakeFiles);
        _sut.SourceFolderPath = Path.GetTempPath();

        Assert.Single(_sut.Artists);
        Assert.Equal("(Other)", _sut.Artists[0].Name);
        Assert.Equal("(Other)", _sut.Artists[0].Albums[0].Name);
    }

    [Fact]
    public void Refresh_ClearsSelectedArtistAndAlbum()
    {
        var fakeFiles = new List<MusicFile>
        {
            new() { FileName = "Beatles/Abbey Road/Come Together.mp3", FullPath = "/music/Beatles/Abbey Road/Come Together.mp3", FileSizeBytes = 1000 },
        }.AsReadOnly();
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(fakeFiles);
        _sut.SourceFolderPath = Path.GetTempPath();

        // Select an artist/album
        _sut.SelectedArtist = _sut.Artists[0];
        _sut.SelectedAlbum = _sut.Artists[0].Albums[0];

        // Refresh again
        _sut.RefreshCommand.Execute(null);

        Assert.Null(_sut.SelectedArtist);
        Assert.Null(_sut.SelectedAlbum);
    }

    // --- Selection state ---

    [Fact]
    public void HasArtistSelection_IsFalse_WhenNoArtistSelected()
    {
        Assert.False(_sut.HasArtistSelection);
    }

    [Fact]
    public void HasArtistSelection_IsTrue_WhenArtistSelected()
    {
        var artist = new LibraryArtistViewModel("Beatles", []);
        _sut.SelectedArtist = artist;

        Assert.True(_sut.HasArtistSelection);
    }

    [Fact]
    public void SelectedArtist_WhenChanged_ClearsSelectedAlbum()
    {
        var album = new LibraryAlbumViewModel("Abbey Road", []);
        _sut.SelectedAlbum = album;

        _sut.SelectedArtist = new LibraryArtistViewModel("Beatles", [album]);

        Assert.Null(_sut.SelectedAlbum);
    }

    [Fact]
    public void CurrentAlbums_ReflectsSelectedArtistAlbums()
    {
        var album1 = new LibraryAlbumViewModel("Abbey Road", []);
        var album2 = new LibraryAlbumViewModel("Help", []);
        var artist = new LibraryArtistViewModel("Beatles", [album1, album2]);
        _sut.SelectedArtist = artist;

        Assert.Equal(2, _sut.CurrentAlbums.Count);
    }

    [Fact]
    public void HasSelectedAlbum_IsFalse_WhenNoAlbumSelected()
    {
        Assert.False(_sut.HasSelectedAlbum);
    }

    [Fact]
    public void HasSelectedAlbum_IsTrue_WhenAlbumSelected()
    {
        _sut.SelectedAlbum = new LibraryAlbumViewModel("Abbey Road", []);

        Assert.True(_sut.HasSelectedAlbum);
    }

    [Fact]
    public void SelectedAlbum_WhenChanged_ClearsSelectedFiles()
    {
        var file = new MusicFile { FileName = "a.mp3", FullPath = "/music/a.mp3", FileSizeBytes = 1 };
        _sut.SelectedFiles.Add(file);
        Assert.True(_sut.HasSelectedFiles); // precondition

        _sut.SelectedAlbum = new LibraryAlbumViewModel("New Album", []);

        Assert.Empty(_sut.SelectedFiles);
    }

    [Fact]
    public void CurrentSongs_ReflectsSelectedAlbumSongs()
    {
        var song1 = new MusicFile { FileName = "a.mp3", FullPath = "/m/a.mp3", FileSizeBytes = 1 };
        var song2 = new MusicFile { FileName = "b.mp3", FullPath = "/m/b.mp3", FileSizeBytes = 2 };
        _sut.SelectedAlbum = new LibraryAlbumViewModel("Abbey Road", [song1, song2]);

        Assert.Equal(2, _sut.CurrentSongs.Count);
    }

    [Fact]
    public void PropertyChanged_RaisedForHasSelectedAlbum_WhenAlbumSet()
    {
        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.SelectedAlbum = new LibraryAlbumViewModel("Album", []);

        Assert.Contains(nameof(LibraryViewModel.HasSelectedAlbum), raised);
    }

    [Fact]
    public void PropertyChanged_RaisedForHasFiles_WhenArtistsReplaced()
    {
        var fakeFiles = new List<MusicFile>
        {
            new() { FileName = "Beatles/Abbey Road/Come Together.mp3", FullPath = "/music/Beatles/Abbey Road/Come Together.mp3", FileSizeBytes = 1000 },
        }.AsReadOnly();
        _fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(fakeFiles);

        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.SourceFolderPath = Path.GetTempPath();

        Assert.Contains(nameof(LibraryViewModel.HasFiles), raised);
    }

    // --- SelectedFiles ---

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
        Assert.False(_sut.HasSelectedFiles);
    }

    [Fact]
    public void SelectedFiles_CollectionChanged_UpdatesHasSelectedFiles()
    {
        var file = new MusicFile { FileName = "a.mp3", FullPath = @"C:\Music\a.mp3", FileSizeBytes = 1000 };

        _sut.SelectedFiles.Add(file);

        Assert.True(_sut.HasSelectedFiles);
    }

    [Fact]
    public void SelectedFiles_CollectionChanged_ToEmpty_UpdatesHasSelectedFiles()
    {
        var file = new MusicFile { FileName = "a.mp3", FullPath = @"C:\Music\a.mp3", FileSizeBytes = 1000 };
        _sut.SelectedFiles.Add(file);
        Assert.True(_sut.HasSelectedFiles);

        _sut.SelectedFiles.Remove(file);

        Assert.False(_sut.HasSelectedFiles);
    }

    [Fact]
    public void SelectedFiles_InitialCollection_PropertyChangedRaised_WhenItemAdded()
    {
        var sut = new LibraryViewModel(_fileTransfer);
        var raisedNames = new List<string?>();
        sut.PropertyChanged += (_, e) => raisedNames.Add(e.PropertyName);

        sut.SelectedFiles.Add(new MusicFile { FileName = "a.mp3", FullPath = @"C:\Music\a.mp3", FileSizeBytes = 1 });

        Assert.Contains("HasSelectedFiles", raisedNames);
    }

    [Fact]
    public void SelectedFiles_InitialCollection_PropertyChangedRaised_WhenItemRemoved()
    {
        var sut = new LibraryViewModel(_fileTransfer);
        var file = new MusicFile { FileName = "a.mp3", FullPath = @"C:\Music\a.mp3", FileSizeBytes = 1 };
        sut.SelectedFiles.Add(file);

        var raisedNames = new List<string?>();
        sut.PropertyChanged += (_, e) => raisedNames.Add(e.PropertyName);

        sut.SelectedFiles.Remove(file);

        Assert.Contains("HasSelectedFiles", raisedNames);
    }
}
