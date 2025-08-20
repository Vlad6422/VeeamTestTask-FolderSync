using FolderSync.Contracts;
using log4net;

namespace FolderSync.Core
{
    internal sealed class ReplicaMaintenance : IReplicaMaintenance
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ReplicaMaintenance));

        private readonly IFolderSnapshotProvider _snapshotProvider;

        public ReplicaMaintenance(IFolderSnapshotProvider snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
        }

        public void DeleteExtraneousFiles(string sourceRoot, string replicaRoot, IReadOnlyDictionary<string, DateTime> currentSourceSnapshot)
        {
            var replicaSnapshot = _snapshotProvider.GetSnapshot(replicaRoot);
            var extraneous = replicaSnapshot.Keys
                .Except(currentSourceSnapshot.Keys, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var relative in extraneous)
            {
                var fullPath = Path.Combine(replicaRoot, relative);
                try
                {
                    File.Delete(fullPath);
                    Logger.Info($"Deleted extraneous file '{fullPath}'.");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error deleting extraneous file '{fullPath}': {ex.Message}", ex);
                }
            }
        }

        public void PruneEmptyDirectories(string replicaRoot)
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(replicaRoot, "*", SearchOption.AllDirectories)
                                             .OrderByDescending(d => d.Length))
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir);
                        Logger.Debug($"Removed empty directory '{dir}'.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Directory pruning encountered an issue: {ex.Message}");
            }
        }
    }
}