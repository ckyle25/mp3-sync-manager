namespace Mp3SyncManager.ViewModels;

public class LibraryArtistViewModel
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<LibraryAlbumViewModel> Albums { get; init; } = [];
}
