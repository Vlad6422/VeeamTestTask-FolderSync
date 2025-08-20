using FolderSync.Core;
using FolderSync.Contracts;

namespace FolderSync.Tests.Unit;

public class FolderSnapshotProviderTests : IDisposable
{
    private readonly string _root;
    private readonly FolderSnapshotProvider _provider = new();

    public FolderSnapshotProviderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "SSP_" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void GetSnapshot_EmptyFolder_ReturnsEmpty()
    {
        var snap = _provider.GetSnapshot(_root);
        Assert.Empty(snap);
    }

    [Fact]
    public void GetSnapshot_IncludesNestedRelativePaths()
    {
        var nested = Path.Combine(_root, "a", "b");
        Directory.CreateDirectory(nested);
        var f1 = Path.Combine(_root, "file1.txt");
        var f2 = Path.Combine(nested, "file2.txt");
        File.WriteAllText(f1, "X");
        File.WriteAllText(f2, "Y");

        var snap = _provider.GetSnapshot(_root);
        Assert.Equal(2, snap.Count);
        Assert.Contains("file1.txt", snap.Keys);
        Assert.Contains(Path.Combine("a", "b", "file2.txt"), snap.Keys);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
