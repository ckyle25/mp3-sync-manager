using Avalonia.Media.Imaging;

namespace Mp3SyncManager.Services.Interfaces;

public interface IAlbumArtService
{
    Task<Bitmap?> GetAlbumArtAsync(string folderPath);
}
