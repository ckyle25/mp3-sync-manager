using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mp3SyncManager.Models;
using Mp3SyncManager.Services.Interfaces;

namespace Mp3SyncManager.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    private readonly IFileTransferService _fileTransfer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceFolderName))]
    private string _sourceFolderPath = string.Empty;

    public string SourceFolderName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SourceFolderPath)) return string.Empty;
            var trimmed = SourceFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? trimmed : name;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyLibraryMessage))]
    private bool _sourceFolderMissing;

    // HasFiles and ShowEmptyLibraryMessage are defined in LibraryViewModel.HasFiles.cs
    // and depend on Artists.Count. Replacing Artists raises both via [NotifyPropertyChangedFor].
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFiles))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyLibraryMessage))]
    private ObservableCollection<LibraryArtistViewModel> _artists = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentAlbums))]
    [NotifyPropertyChangedFor(nameof(HasArtistSelection))]
    private LibraryArtistViewModel? _selectedArtist;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSongs))]
    [NotifyPropertyChangedFor(nameof(HasSelectedAlbum))]
    private LibraryAlbumViewModel? _selectedAlbum;

    public ObservableCollection<LibraryAlbumViewModel> CurrentAlbums =>
        SelectedArtist?.Albums ?? [];

    public ObservableCollection<MusicFile> CurrentSongs =>
        SelectedAlbum?.Songs ?? [];

    public bool HasArtistSelection => SelectedArtist is not null;
    public bool HasSelectedAlbum => SelectedAlbum is not null;

    [ObservableProperty]
    private ObservableCollection<MusicFile> _selectedFiles = [];

    public bool HasSelectedFiles => SelectedFiles.Count > 0;

    public LibraryViewModel(IFileTransferService fileTransfer)
    {
        _fileTransfer = fileTransfer;
        SubscribeSelectedFiles(_selectedFiles);
    }

    partial void OnSourceFolderPathChanged(string value)
    {
        SourceFolderMissing = !string.IsNullOrWhiteSpace(value) && !Directory.Exists(value);
        Refresh();
    }

    partial void OnSelectedArtistChanged(LibraryArtistViewModel? value)
    {
        SelectedAlbum = null;
    }

    partial void OnSelectedAlbumChanged(LibraryAlbumViewModel? value)
    {
        SelectedFiles.Clear();
    }

    [RelayCommand]
    public void Refresh()
    {
        if (string.IsNullOrWhiteSpace(SourceFolderPath)) return;

        var files = _fileTransfer.ListFiles(SourceFolderPath, displayRelativePaths: true);

        // FileName is the relative path from ListFiles(displayRelativePaths:true),
        // e.g. "Beatles/Abbey Road/Come Together.mp3" on Linux or
        // "Beatles\Abbey Road\Come Together.mp3" on Windows.
        // We split on both separators; all parts except the last are directory segments.
        var artists = files
            .GroupBy(f => GetSegment(f.FileName, 0))
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(ag => new LibraryArtistViewModel(
                ag.Key,
                ag.GroupBy(f => GetSegment(f.FileName, 1))
                  .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                  .Select(albumGroup => new LibraryAlbumViewModel(
                      albumGroup.Key,
                      albumGroup
                          .OrderBy(f => Path.GetFileName(f.FullPath), StringComparer.OrdinalIgnoreCase)
                          .Select(f => new MusicFile
                          {
                              FileName = Path.GetFileName(f.FullPath),
                              FullPath = f.FullPath,
                              FileSizeBytes = f.FileSizeBytes,
                          })))))
            .ToList();

        SelectedArtist = null;
        SelectedAlbum = null;
        Artists = new ObservableCollection<LibraryArtistViewModel>(artists);
    }

    // relativeFileName is the relative path from ListFiles(displayRelativePaths:true).
    // All parts except the last are directory segments; the last is the filename.
    // Returns the directory segment at segmentIndex, or "(Other)" if the file is too shallow.
    private static string GetSegment(string relativeFileName, int segmentIndex)
    {
        var parts = relativeFileName.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return segmentIndex < parts.Length - 1 ? parts[segmentIndex] : "(Other)";
    }
}
