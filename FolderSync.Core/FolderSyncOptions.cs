namespace FolderSync.Core
{
    public sealed class FolderSyncOptions
    {
        public string SourceFolder { get; }
        public string ReplicaFolder { get; }
        public int SyncIntervalMilliseconds { get; }
        public string LogPath { get; }

        public FolderSyncOptions(string sourceFolder, string replicaFolder, int syncIntervalMilliseconds, string logPath)
        {
            SourceFolder = sourceFolder;
            ReplicaFolder = replicaFolder;
            SyncIntervalMilliseconds = syncIntervalMilliseconds;
            LogPath = logPath;
        }

        internal (string Source, string Replica, int IntervalMs, string LogFile) NormalizeAndValidate()
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(SourceFolder, nameof(SourceFolder));
            ArgumentException.ThrowIfNullOrWhiteSpace(ReplicaFolder, nameof(ReplicaFolder));
            ArgumentException.ThrowIfNullOrWhiteSpace(LogPath, nameof(LogPath));
            if (SyncIntervalMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(SyncIntervalMilliseconds), "Sync interval must be > 0 (milliseconds).");

            if (!Directory.Exists(SourceFolder))
                throw new DirectoryNotFoundException($"Source folder '{SourceFolder}' does not exist.");

            if (!Directory.Exists(ReplicaFolder))
                Directory.CreateDirectory(ReplicaFolder);

            string finalLogFilePath;
            if (Directory.Exists(LogPath) || !Path.HasExtension(LogPath))
            {
                if (!Directory.Exists(LogPath))
                {
                    Directory.CreateDirectory(LogPath);
                }
                finalLogFilePath = Path.Combine(LogPath, "file.log");
            }
            else
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (string.IsNullOrWhiteSpace(dir))
                    throw new ArgumentException("Log file path must include a directory.", nameof(LogPath));
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                finalLogFilePath = LogPath;
            }

            return (Path.GetFullPath(SourceFolder),
                    Path.GetFullPath(ReplicaFolder),
                    SyncIntervalMilliseconds,
                    Path.GetFullPath(finalLogFilePath));
        }
    }
}