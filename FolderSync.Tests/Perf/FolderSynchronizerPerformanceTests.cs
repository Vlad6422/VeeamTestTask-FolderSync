using System.Diagnostics;
using FolderSync.Core;
using Xunit.Abstractions;

namespace FolderSync.Tests.Perf;

public class FolderSynchronizerPerformanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FSPERF_" + Guid.NewGuid());
    private readonly string _source;
    private readonly string _replica;
    private readonly string _logs;
    private readonly ITestOutputHelper _output;

    public FolderSynchronizerPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _source = Path.Combine(_root, "src");
        _replica = Path.Combine(_root, "rep");
        _logs = Path.Combine(_root, "logs");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_replica);
        Directory.CreateDirectory(_logs);
    }

    [Fact]
    public void SyncFolder_Copies_10000_Files()
    {
        const int fileCount = 10_000;

        // Generate 10k files distributed across subdirectories to avoid a huge single directory.
        for (int i = 0; i < fileCount; i++)
        {
            var subDir = Path.Combine(_source, (i / 100).ToString("D3")); // 100 files per dir
            if (!Directory.Exists(subDir)) Directory.CreateDirectory(subDir);
            var filePath = Path.Combine(subDir, $"file_{i:D5}.txt");
            File.WriteAllText(filePath, $"CONTENT_{i}");
        }

        var sync = new FolderSynchronizer(_source, _replica, 1_000, _logs);
        var sw = Stopwatch.StartNew();
        sync.SyncFolder();
        sw.Stop();

        // Collect relative file maps
        var sourceFiles = Directory.GetFiles(_source, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(_source, p).Replace('\\', '/'))
            .OrderBy(p => p)
            .ToList();
        var replicaFiles = Directory.GetFiles(_replica, "*", SearchOption.AllDirectories)
            .Where(p => !IsInternalStateFile(Path.GetFileName(p)))
            .Select(p => Path.GetRelativePath(_replica, p).Replace('\\', '/'))
            .OrderBy(p => p)
            .ToList();

        Assert.Equal(fileCount, sourceFiles.Count);
        Assert.Equal(sourceFiles.Count, replicaFiles.Count);
        Assert.Equal(sourceFiles, replicaFiles);

        // Spot-check contents for a few deterministic indices
        foreach (var i in new[] { 0, fileCount / 2, fileCount - 1 })
        {
            var rel = ((i / 100).ToString("D3") + "/" + $"file_{i:D5}.txt");
            var replicaPath = Path.Combine(_replica, rel.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(replicaPath), $"Replica missing {rel}");
            Assert.Equal($"CONTENT_{i}", File.ReadAllText(replicaPath));
        }

        _output.WriteLine($"Synchronized {fileCount} files in {sw.Elapsed.TotalSeconds:F2}s");
    }

    private static bool IsInternalStateFile(string fileName)
    {
        if (!fileName.StartsWith(".foldersync.", StringComparison.OrdinalIgnoreCase)) return false;
        return fileName.EndsWith(".snapshot.json", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".snap", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".snaps", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
