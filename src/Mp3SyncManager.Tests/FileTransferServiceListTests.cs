using Mp3SyncManager.Services;
using Mp3SyncManager.Services.Interfaces;
using NSubstitute;

namespace Mp3SyncManager.Tests;

public class FileTransferServiceListTests : IDisposable
{
    private readonly string _testRoot;
    private readonly FileTransferService _sut;

    public FileTransferServiceListTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Mp3SyncManagerListTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);

        var deviceDetection = Substitute.For<IDeviceDetectionService>();
        _sut = new FileTransferService(deviceDetection);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [Fact]
    public async Task ListFiles_RecursesIntoSubfolders_ReturnsAllFiles()
    {
        // Arrange: two files in root, one in a subfolder
        var rootFile1 = Path.Combine(_testRoot, "alpha.mp3");
        var rootFile2 = Path.Combine(_testRoot, "beta.mp3");
        var subDir = Path.Combine(_testRoot, "Artist");
        Directory.CreateDirectory(subDir);
        var subFile = Path.Combine(subDir, "track.mp3");

        await File.WriteAllTextAsync(rootFile1, "data");
        await File.WriteAllTextAsync(rootFile2, "data");
        await File.WriteAllTextAsync(subFile, "data");

        // Act
        var results = _sut.ListFiles(_testRoot, displayRelativePaths: true);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, f => f.FullPath == rootFile1);
        Assert.Contains(results, f => f.FullPath == rootFile2);
        Assert.Contains(results, f => f.FullPath == subFile);
    }

    [Fact]
    public async Task ListFiles_DuplicateFileNames_BothReturnedWithDistinctRelativePaths()
    {
        // Arrange: same filename in two different subdirectories
        var dir1 = Path.Combine(_testRoot, "Artist1");
        var dir2 = Path.Combine(_testRoot, "Artist2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        var file1 = Path.Combine(dir1, "song.mp3");
        var file2 = Path.Combine(dir2, "song.mp3");
        await File.WriteAllTextAsync(file1, "data");
        await File.WriteAllTextAsync(file2, "data");

        // Act
        var results = _sut.ListFiles(_testRoot, displayRelativePaths: true);

        // Assert: two entries, distinct FileName values (relative paths)
        Assert.Equal(2, results.Count);
        var fileNames = results.Select(f => f.FileName).ToList();
        Assert.Equal(2, fileNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // Both should contain the subdirectory component
        Assert.All(results, f => Assert.Contains(Path.DirectorySeparatorChar, f.FileName));
    }

    [Fact]
    public async Task ListFiles_FlatMode_SubfolderFile_IsNotIncluded()
    {
        // Arrange: file in a subfolder
        var subDir = Path.Combine(_testRoot, "SubFolder");
        Directory.CreateDirectory(subDir);
        var subFile = Path.Combine(subDir, "track.mp3");
        await File.WriteAllTextAsync(subFile, "data");

        // Act: flat mode (displayRelativePaths: false) enumerates top-level only
        var results = _sut.ListFiles(_testRoot, displayRelativePaths: false);

        // Assert: subfolder file not included
        Assert.Empty(results);
    }

    [Fact]
    public async Task ListFiles_FlatMode_TopLevelFile_HasPlainFileName()
    {
        // Arrange: file directly in root
        var rootFile = Path.Combine(_testRoot, "song.mp3");
        await File.WriteAllTextAsync(rootFile, "data");

        // Act
        var results = _sut.ListFiles(_testRoot, displayRelativePaths: false);

        // Assert: one result, FileName is the plain filename with no directory separator
        Assert.Single(results);
        Assert.DoesNotContain(Path.DirectorySeparatorChar, results[0].FileName);
        Assert.Equal("song.mp3", results[0].FileName);
    }

    [Fact]
    public async Task ListFiles_Device_RecursiveWithRelativePaths()
    {
        // Arrange: files at deviceRoot\Artist\song1.mp3 and deviceRoot\Artist\Album\song2.mp3
        var artistDir = Path.Combine(_testRoot, "Artist");
        var albumDir = Path.Combine(artistDir, "Album");
        Directory.CreateDirectory(albumDir);

        var file1 = Path.Combine(artistDir, "song1.mp3");
        var file2 = Path.Combine(albumDir, "song2.mp3");
        await File.WriteAllTextAsync(file1, "data1");
        await File.WriteAllTextAsync(file2, "data2");

        // Act: device listing is now recursive with relative paths
        var results = _sut.ListFiles(_testRoot, displayRelativePaths: true);

        // Assert: both files returned with relative-path FileName values
        Assert.Equal(2, results.Count);

        var expectedRel1 = Path.Combine("Artist", "song1.mp3");
        var expectedRel2 = Path.Combine("Artist", "Album", "song2.mp3");
        Assert.Contains(results, f => f.FileName == expectedRel1 && f.FullPath == file1);
        Assert.Contains(results, f => f.FileName == expectedRel2 && f.FullPath == file2);
    }

    [Fact]
    public void ListFiles_NonExistentFolder_ReturnsEmpty()
    {
        var missing = Path.Combine(_testRoot, "does-not-exist");
        var results = _sut.ListFiles(missing, displayRelativePaths: true);
        Assert.Empty(results);
    }

    [Fact]
    public async Task ListFiles_NestedSubfolders_ReturnsFilesFromAllLevels()
    {
        // Arrange: three levels deep
        var level1 = Path.Combine(_testRoot, "Level1");
        var level2 = Path.Combine(level1, "Level2");
        Directory.CreateDirectory(level2);

        var rootFile = Path.Combine(_testRoot, "root.mp3");
        var l1File = Path.Combine(level1, "l1.mp3");
        var l2File = Path.Combine(level2, "l2.mp3");
        await File.WriteAllTextAsync(rootFile, "data");
        await File.WriteAllTextAsync(l1File, "data");
        await File.WriteAllTextAsync(l2File, "data");

        // Act
        var results = _sut.ListFiles(_testRoot, displayRelativePaths: true);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, f => f.FullPath == rootFile);
        Assert.Contains(results, f => f.FullPath == l1File);
        Assert.Contains(results, f => f.FullPath == l2File);
    }

    [Fact]
    public async Task ListFiles_NoDuplicates_WhenScanCalledOnSameFolder()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "a.mp3"), "data");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "b.mp3"), "data");

        // Act: call twice
        var results1 = _sut.ListFiles(_testRoot, displayRelativePaths: true);
        var results2 = _sut.ListFiles(_testRoot, displayRelativePaths: true);

        // Assert: no duplicates within either call
        Assert.Equal(results1.Count, results1.Select(f => f.FullPath).Distinct().Count());
        Assert.Equal(2, results1.Count);
        Assert.Equal(2, results2.Count);
    }

    [Fact]
    public async Task ListFiles_DefaultParameterValue_IsFlatEnumeration()
    {
        // Arrange: file only in a subfolder, nothing at root level
        var subDir = Path.Combine(_testRoot, "SubFolder");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "nested.mp3"), "data");

        // Act: call with no optional args — defaults are searchPattern "*.mp3", displayRelativePaths false
        var results = _sut.ListFiles(_testRoot);

        // Assert: flat enumeration returns nothing because the file is not at root level
        Assert.Empty(results);
    }

    [Fact]
    public async Task ListFiles_RootLevelFile_RelativePathIsPlainFilename()
    {
        // Arrange: single file directly in the root folder
        var rootFile = Path.Combine(_testRoot, "root.mp3");
        await File.WriteAllTextAsync(rootFile, "data");

        // Act
        var results = _sut.ListFiles(_testRoot, displayRelativePaths: true);

        // Assert: FileName is the bare filename, not prefixed with "." or a separator
        Assert.Single(results);
        Assert.Equal("root.mp3", results[0].FileName);
    }

    [Fact]
    public void ListFiles_EmptySubdirectory_DoesNotContributeEntries()
    {
        // Arrange: subdirectory exists but contains no files
        var emptyDir = Path.Combine(_testRoot, "EmptyAlbum");
        Directory.CreateDirectory(emptyDir);

        // Act
        var results = _sut.ListFiles(_testRoot, displayRelativePaths: true);

        // Assert: empty directory contributes no entries
        Assert.Empty(results);
    }

    [Fact]
    public async Task ListFiles_DeepNesting_RelativePathsAreCorrect()
    {
        // Arrange: file at depth-3 (a\b\c\deep.mp3)
        var deepDir = Path.Combine(_testRoot, "a", "b", "c");
        Directory.CreateDirectory(deepDir);
        var deepFile = Path.Combine(deepDir, "deep.mp3");
        await File.WriteAllTextAsync(deepFile, "data");

        // Act
        var results = _sut.ListFiles(_testRoot, displayRelativePaths: true);

        // Assert: single result whose FileName has at least two separators (a\b\c\deep.mp3)
        Assert.Single(results);
        var separatorCount = results[0].FileName.Count(c => c == Path.DirectorySeparatorChar);
        Assert.True(separatorCount >= 2, $"Expected at least 2 separators in '{results[0].FileName}'");

        // FullPath must be absolute (not relative)
        Assert.True(Path.IsPathRooted(results[0].FullPath), $"Expected absolute path, got '{results[0].FullPath}'");
        Assert.Equal(deepFile, results[0].FullPath);
    }
}
