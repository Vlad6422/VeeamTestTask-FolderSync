using FolderSync.Core;

namespace FolderSync.Tests.Unit;

public class RetryingFileCopyServiceTests : IDisposable
{
    private readonly string _root;
    private readonly RetryingFileCopyService _svc = new();

    public RetryingFileCopyServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "RFC_" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void CopyFile_BasicCopy_Works()
    {
        var src = Path.Combine(_root, "a.txt");
        var dst = Path.Combine(_root, "b.txt");
        File.WriteAllText(src, "DATA");
        _svc.CopyFile(src, dst, overwrite: true);
        Assert.Equal("DATA", File.ReadAllText(dst));
    }

    [Fact]
    public void CopyFile_Overwrites_WhenOverwriteTrue()
    {
        var src = Path.Combine(_root, "src.txt");
        var dst = Path.Combine(_root, "dst.txt");
        File.WriteAllText(src, "V1");
        File.WriteAllText(dst, "OLD");
        _svc.CopyFile(src, dst, overwrite: true);
        Assert.Equal("V1", File.ReadAllText(dst));
    }

    [Fact]
    public void CopyFile_Throws_WhenSourceMissing()
    {
        var missing = Path.Combine(_root, "missing.txt");
        var dst = Path.Combine(_root, "out.txt");
        Assert.Throws<FileNotFoundException>(() => _svc.CopyFile(missing, dst, overwrite: true));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
