using Mp3SyncManager.Models;

namespace Mp3SyncManager.ViewModels;

public class LibraryAlbumViewModel
{
    public string ArtistName { get; init; } = string.Empty;
    public string AlbumName { get; init; } = string.Empty;
    public IReadOnlyList<MusicFile> Songs { get; init; } = [];
}
