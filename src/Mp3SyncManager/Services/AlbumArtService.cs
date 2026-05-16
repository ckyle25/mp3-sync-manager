using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using TagLib;

namespace Mp3SyncManager.Services;

public class AlbumArtService : Interfaces.IAlbumArtService, IDisposable
{
    private readonly ConcurrentDictionary<string, Bitmap?> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    // Common well-known names checked first (case-insensitive on Windows via File.Exists/NTFS).
    private static readonly string[] NamedImageFiles =
    [
        "folder.jpg",   "folder.jpeg",   "folder.png",
        "cover.jpg",    "cover.jpeg",    "cover.png",
        "front.jpg",    "front.jpeg",    "front.png",
        "album.jpg",    "album.jpeg",    "album.png",
        "artwork.jpg",  "artwork.jpeg",  "artwork.png",
        "art.jpg",      "art.jpeg",      "art.png",
        "thumb.jpg",    "thumb.jpeg",    "thumb.png",
    ];

    private static readonly string[] ImageGlobs = ["*.jpg", "*.jpeg", "*.png"];

    public Task<Bitmap?> GetAlbumArtAsync(string folderPath)
    {
        if (_cache.TryGetValue(folderPath, out var cached))
            return Task.FromResult(cached);

        return Task.Run(() => LoadAndCache(folderPath));
    }

    private Bitmap? LoadAndCache(string folderPath)
    {
        Bitmap? result = null;
        try
        {
            result = LoadFromNamedImages(folderPath)
                  ?? LoadFromAnyImage(folderPath)
                  ?? LoadFromMp3Tags(folderPath)
                  ?? LoadFromParentFolder(folderPath);
        }
        catch { }

        _cache.TryAdd(folderPath, result);
        return result;
    }

    // 1. Check well-known filenames in the album folder.
    private static Bitmap? LoadFromNamedImages(string folderPath)
    {
        foreach (var name in NamedImageFiles)
        {
            var path = Path.Combine(folderPath, name);
            if (System.IO.File.Exists(path))
            {
                try { return new Bitmap(path); }
                catch { }
            }
        }
        return null;
    }

    // 2. Fall back to any image file in the folder — pick the largest (most likely full album art,
    //    not a small icon), filtering out files under 10 KB.
    private static Bitmap? LoadFromAnyImage(string folderPath)
    {
        foreach (var glob in ImageGlobs)
        {
            try
            {
                var candidate = Directory
                    .EnumerateFiles(folderPath, glob, SearchOption.TopDirectoryOnly)
                    .Select(p => new FileInfo(p))
                    .Where(fi => fi.Length > 10_000)
                    .OrderByDescending(fi => fi.Length)
                    .FirstOrDefault();

                if (candidate is not null)
                    return new Bitmap(candidate.FullName);
            }
            catch { }
        }
        return null;
    }

    // 3. Read embedded ID3 art from every .mp3 in the folder (stop at first hit).
    //    Caps at 10 files so a huge flat folder doesn't stall the background task.
    private static Bitmap? LoadFromMp3Tags(string folderPath)
    {
        try
        {
            foreach (var mp3 in Directory.EnumerateFiles(folderPath, "*.mp3").Take(10))
            {
                try
                {
                    using var file = TagLib.File.Create(mp3);
                    var pic = file.Tag.Pictures.FirstOrDefault();
                    if (pic is not null)
                        return new Bitmap(new MemoryStream(pic.Data.Data));
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    // 4. Try the parent directory (artist-level art) using only named images.
    private static Bitmap? LoadFromParentFolder(string folderPath)
    {
        var parent = Path.GetDirectoryName(folderPath);
        if (parent is null || string.Equals(parent, folderPath, StringComparison.OrdinalIgnoreCase))
            return null;
        return LoadFromNamedImages(parent) ?? LoadFromAnyImage(parent);
    }

    public void Dispose()
    {
        foreach (var bitmap in _cache.Values)
            bitmap?.Dispose();
        _cache.Clear();
    }
}
