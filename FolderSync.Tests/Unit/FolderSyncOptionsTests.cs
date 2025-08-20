using FolderSync.Core;

namespace FolderSync.Tests.Unit;

public class FolderSyncOptionsTests
{
    [Fact]
    public void NormalizeAndValidate_ReturnsFullPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "FSO_" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "src");
        var replica = Path.Combine(root, "rep");
        var logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(replica);

        var opts = new FolderSyncOptions(source, replica, 1000, logs);
        var (s, r, interval, log) = opts.NormalizeAndValidate();

        Assert.Equal(Path.GetFullPath(source), s);
        Assert.Equal(Path.GetFullPath(replica), r);
        Assert.Equal(1000, interval);
        Assert.Equal(Path.Combine(Path.GetFullPath(logs), "file.log"), log);
    }

    [Fact]
    public void NormalizeAndValidate_Throws_WhenSourceMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "FSO_" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var replica = Path.Combine(root, "rep");
        Directory.CreateDirectory(replica);
        var logs = Path.Combine(root, "logs");

        var opts = new FolderSyncOptions(Path.Combine(root, "missing"), replica, 100, logs);
        Assert.Throws<DirectoryNotFoundException>(() => opts.NormalizeAndValidate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NormalizeAndValidate_Throws_OnNonPositiveInterval(int interval)
    {
        var root = Path.Combine(Path.GetTempPath(), "FSO_" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "src");
        var replica = Path.Combine(root, "rep");
        var logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(replica);

        var opts = new FolderSyncOptions(source, replica, interval, logs);
        Assert.Throws<ArgumentOutOfRangeException>(() => opts.NormalizeAndValidate());
    }
}
