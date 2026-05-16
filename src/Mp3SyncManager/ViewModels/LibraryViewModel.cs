using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mp3SyncManager.Models;
using Mp3SyncManager.Services.Interfaces;

namespace Mp3SyncManager.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    private readonly IFileTransferService _fileTransfer;
    private readonly IAudioPlayerService _audioPlayer;
    private readonly IAlbumArtService _albumArtService;

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

    [ObservableProperty]
    private Bitmap? _selectedAlbumArt;

    public ObservableCollection<LibraryAlbumViewModel> CurrentAlbums =>
        SelectedArtist?.Albums ?? [];

    public ObservableCollection<MusicFile> CurrentSongs =>
        SelectedAlbum?.Songs ?? [];

    public bool HasArtistSelection => SelectedArtist is not null;
    public bool HasSelectedAlbum => SelectedAlbum is not null;

    [ObservableProperty]
    private ObservableCollection<MusicFile> _selectedFiles = [];

    public bool HasSelectedFiles => SelectedFiles.Count > 0;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaybackBarVisible))]
    [NotifyPropertyChangedFor(nameof(PlayPauseLabel))]
    private AudioPlaybackState _playbackState = AudioPlaybackState.Stopped;

    [ObservableProperty]
    private string? _nowPlayingName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackProgressPercent))]
    [NotifyPropertyChangedFor(nameof(PlaybackPositionText))]
    private TimeSpan _playbackPosition;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackProgressPercent))]
    [NotifyPropertyChangedFor(nameof(PlaybackPositionText))]
    private TimeSpan _playbackDuration;

    public bool IsPlaybackBarVisible => PlaybackState != AudioPlaybackState.Stopped;
    public string PlayPauseLabel => PlaybackState == AudioPlaybackState.Playing ? "Pause" : "Play";
    public double PlaybackProgressPercent =>
        PlaybackDuration.TotalSeconds > 0
            ? PlaybackPosition.TotalSeconds / PlaybackDuration.TotalSeconds * 100
            : 0;
    public string PlaybackPositionText =>
        $"{FormatTime(PlaybackPosition)} / {FormatTime(PlaybackDuration)}";

    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";

    public LibraryViewModel(IFileTransferService fileTransfer, IAudioPlayerService audioPlayer, IAlbumArtService albumArtService)
    {
        _fileTransfer = fileTransfer;
        _audioPlayer = audioPlayer;
        _albumArtService = albumArtService;

        _audioPlayer.PlaybackEnded += (_, _) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(ResetPlaybackState);
        _audioPlayer.PositionChanged += (_, _) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                PlaybackPosition = _audioPlayer.Position);

        SubscribeSelectedFiles(_selectedFiles);
    }

    private bool CanPlaySelected() => SelectedFiles.Count == 1;

    [RelayCommand(CanExecute = nameof(CanPlaySelected))]
    private void PlaySelected()
    {
        var file = SelectedFiles[0];
        _audioPlayer.Play(file.FullPath);
        NowPlayingName = file.FileName;
        PlaybackDuration = _audioPlayer.Duration;
        PlaybackState = AudioPlaybackState.Playing;
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (PlaybackState == AudioPlaybackState.Playing)
        {
            _audioPlayer.Pause();
            PlaybackState = AudioPlaybackState.Paused;
        }
        else if (PlaybackState == AudioPlaybackState.Paused)
        {
            _audioPlayer.Resume();
            PlaybackState = AudioPlaybackState.Playing;
        }
    }

    [RelayCommand]
    private void StopPlayback()
    {
        _audioPlayer.Stop();
        ResetPlaybackState();
    }

    private void ResetPlaybackState()
    {
        PlaybackState = AudioPlaybackState.Stopped;
        NowPlayingName = null;
        PlaybackPosition = TimeSpan.Zero;
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
        SelectedAlbumArt = value?.AlbumArt;
        if (value is not null && value.AlbumArt is null && value.Songs.Count > 0)
            _ = LoadSelectedAlbumArtAsync(value);
    }

    // Synchronous refresh — used directly by OnSourceFolderPathChanged and in tests.
    // The Refresh button binds to RefreshCommand which is generated from RefreshAsync().
    public void Refresh()
    {
        if (string.IsNullOrWhiteSpace(SourceFolderPath)) return;

        var artists = BuildArtistList(SourceFolderPath);

        SelectedArtist = null;
        SelectedAlbum = null;
        Artists = new ObservableCollection<LibraryArtistViewModel>(artists);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceFolderPath)) return;

        IsLoading = true;
        var sourcePath = SourceFolderPath;
        try
        {
            var artists = await Task.Run(() => BuildArtistList(sourcePath));
            SelectedArtist = null;
            SelectedAlbum = null;
            Artists = new ObservableCollection<LibraryArtistViewModel>(artists);
            _ = LoadAllAlbumArtAsync(artists);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSelectedAlbumArtAsync(LibraryAlbumViewModel album)
    {
        var folderPath = Path.GetDirectoryName(album.Songs[0].FullPath);
        if (folderPath is null) return;

        var art = await _albumArtService.GetAlbumArtAsync(folderPath);
        album.AlbumArt = art;
        if (SelectedAlbum == album)
            SelectedAlbumArt = art;
    }

    private async Task LoadAllAlbumArtAsync(IReadOnlyList<LibraryArtistViewModel> artists)
    {
        foreach (var artist in artists)
        foreach (var album in artist.Albums)
        {
            var folderPath = album.Songs.Count > 0
                ? Path.GetDirectoryName(album.Songs[0].FullPath)
                : null;
            if (folderPath is null) continue;

            var art = await _albumArtService.GetAlbumArtAsync(folderPath);
            Dispatcher.UIThread.Post(() =>
            {
                album.AlbumArt = art;
                if (SelectedAlbum == album)
                    SelectedAlbumArt = art;
            });
        }
    }

    // FileName is the relative path from ListFiles(displayRelativePaths:true),
    // e.g. "Beatles/Abbey Road/Come Together.mp3" on Linux or
    // "Beatles\Abbey Road\Come Together.mp3" on Windows.
    // We split on both separators; all parts except the last are directory segments.
    private List<LibraryArtistViewModel> BuildArtistList(string sourcePath) =>
        _fileTransfer.ListFiles(sourcePath, displayRelativePaths: true)
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
