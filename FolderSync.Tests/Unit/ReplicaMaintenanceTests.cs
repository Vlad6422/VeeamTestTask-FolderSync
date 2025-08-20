using FolderSync.Core;
using FolderSync.Contracts;
using System.Reflection;

namespace FolderSync.Tests.Unit;

public class ReplicaMaintenanceTests : IDisposable
{
    private readonly string _root;
    private readonly string _source;
    private readonly string _replica;
    private readonly IFolderSnapshotProvider _snapshotProvider;
    private readonly ReplicaMaintenance _maintenance;

    public ReplicaMaintenanceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "RM_" + Guid.NewGuid());
        _source = Path.Combine(_root, "src");
        _replica = Path.Combine(_root, "rep");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_replica);
        _snapshotProvider = new FolderSnapshotProvider();
        _maintenance = new ReplicaMaintenance(_snapshotProvider);
    }

    [Fact]
    public void DeleteExtraneousFiles_RemovesOnlyOrphans()
    {
        // Arrange a file present in both source and replica (should stay) and one only in replica (should be deleted)
        var keepSource = Path.Combine(_source, "keep.txt");
        File.WriteAllText(keepSource, "data");
        var keepReplica = Path.Combine(_replica, "keep.txt");
        File.WriteAllText(keepReplica, "stale-version");
        var orphan = Path.Combine(_replica, "orphan.txt");
        File.WriteAllText(orphan, "remove");

        var sourceSnap = _snapshotProvider.GetSnapshot(_source);

        // Act
        _maintenance.DeleteExtraneousFiles(_source, _replica, sourceSnap);

        // Assert
        Assert.True(File.Exists(keepReplica), "Shared file should not be deleted.");
        Assert.False(File.Exists(orphan), "Orphan file should be removed.");
    }

    [Fact]
    public void PruneEmptyDirectories_RemovesNestedEmpty()
    {
        var nested = Path.Combine(_replica, "a", "b", "c");
        Directory.CreateDirectory(nested);
        var occupied = Path.Combine(_replica, "a", "used");
        Directory.CreateDirectory(Path.GetDirectoryName(occupied)!);
        File.WriteAllText(occupied, "X");

        _maintenance.PruneEmptyDirectories(_replica);

        Assert.False(Directory.Exists(nested));
        Assert.True(File.Exists(occupied));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
