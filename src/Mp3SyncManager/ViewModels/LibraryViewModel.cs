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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFiles))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyLibraryMessage))]
    private ObservableCollection<LibraryArtistViewModel> _artists = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArtistSelection))]
    [NotifyPropertyChangedFor(nameof(CurrentAlbums))]
    private LibraryArtistViewModel? _selectedArtist;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedAlbum))]
    [NotifyPropertyChangedFor(nameof(CurrentSongs))]
    private LibraryAlbumViewModel? _selectedAlbum;

    [ObservableProperty]
    private ObservableCollection<MusicFile> _selectedFiles = [];

    public bool HasSelectedFiles => SelectedFiles.Count > 0;
    public bool HasArtistSelection => SelectedArtist is not null;
    public bool HasSelectedAlbum => SelectedAlbum?.Songs.Count > 0;

    public IReadOnlyList<LibraryAlbumViewModel> CurrentAlbums => SelectedArtist?.Albums ?? [];
    public IReadOnlyList<MusicFile> CurrentSongs => SelectedAlbum?.Songs ?? [];

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
        SelectedFiles = [];
    }

    [RelayCommand]
    public void Refresh()
    {
        if (string.IsNullOrWhiteSpace(SourceFolderPath)) return;
        var allFiles = _fileTransfer.ListFiles(SourceFolderPath, displayRelativePaths: false);
        SelectedArtist = null;
        Artists = new ObservableCollection<LibraryArtistViewModel>(GroupFiles(allFiles, SourceFolderPath));
    }

    private static IEnumerable<LibraryArtistViewModel> GroupFiles(IReadOnlyList<MusicFile> files, string sourceRoot)
    {
        return files
            .GroupBy(f => GetSegment(f.FullPath, sourceRoot, 0))
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(artistGroup => new LibraryArtistViewModel
            {
                Name = artistGroup.Key,
                Albums = artistGroup
                    .GroupBy(f => GetSegment(f.FullPath, sourceRoot, 1))
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(albumGroup => new LibraryAlbumViewModel
                    {
                        ArtistName = artistGroup.Key,
                        AlbumName = albumGroup.Key,
                        Songs = albumGroup
                            .OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
                            .ToList()
                            .AsReadOnly()
                    })
                    .ToList()
                    .AsReadOnly()
            });
    }

    private static string GetSegment(string fullPath, string sourceRoot, int depth)
    {
        var rel = Path.GetRelativePath(sourceRoot, fullPath);
        var parts = rel.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= depth + 2 ? parts[depth] : "(Other)";
    }
}
