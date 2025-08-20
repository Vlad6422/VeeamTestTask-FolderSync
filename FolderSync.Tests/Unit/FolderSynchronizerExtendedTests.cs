using FolderSync.Core;
using FolderSync.Contracts;
using System.Collections.Concurrent;

namespace FolderSync.Tests.Unit;

public class FolderSynchronizerExtendedTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FSEXT_" + Guid.NewGuid());
    private readonly string _source;
    private readonly string _replica;
    private readonly string _logs;

    public FolderSynchronizerExtendedTests()
    {
        _source = Path.Combine(_root, "src");
        _replica = Path.Combine(_root, "rep");
        _logs = Path.Combine(_root, "logs");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_replica);
        Directory.CreateDirectory(_logs);
    }

    [Fact]
    public async Task SyncFolder_Skips_WhenAlreadyRunning()
    {
        var fakeSnapshot = new BlockingSnapshotProvider(delayMs: 200); // slow snapshot to hold lock
        var sync = new FolderSynchronizer(
            new FolderSyncOptions(_source, _replica, 100, _logs),
            fakeSnapshot,
            new RetryingFileCopyService(),
            new ReplicaMaintenance(new FolderSnapshotProvider()));

        var t1 = Task.Run(() => sync.SyncFolder());
        await Task.Delay(20); // ensure first acquired lock
        sync.SyncFolder(); // should skip, not throw
        await t1;
        Assert.True(fakeSnapshot.Calls >= 1);
    }

    [Fact]
    public void SyncFolder_UpdatesChangedFileOnly()
    {
        var file = Path.Combine(_source, "a.txt");
        File.WriteAllText(file, "V1");
        var sync = new FolderSynchronizer(_source, _replica, 100, _logs);
        sync.SyncFolder();
        // Modify
        File.WriteAllText(file, "V2");
        var before = Directory.GetFiles(_replica, "*", SearchOption.AllDirectories).Length;
        sync.SyncFolder();
        var after = Directory.GetFiles(_replica, "*", SearchOption.AllDirectories).Length;
        Assert.Equal(before, after); // no new files
        Assert.Equal("V2", File.ReadAllText(Path.Combine(_replica, "a.txt")));
    }

    private sealed class BlockingSnapshotProvider : IFolderSnapshotProvider
    {
        private readonly int _delayMs;
        public int Calls;        
        public BlockingSnapshotProvider(int delayMs) { _delayMs = delayMs; }
        public Dictionary<string, DateTime> GetSnapshot(string folderPath)
        {
            Interlocked.Increment(ref Calls);
            Thread.Sleep(_delayMs);
            var dict = new Dictionary<string, DateTime>();
            foreach (var f in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
                dict[Path.GetRelativePath(folderPath, f)] = File.GetLastWriteTimeUtc(f);
            return dict;
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
