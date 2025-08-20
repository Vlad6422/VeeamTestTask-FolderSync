using FolderSync.Contracts;
using log4net;
using log4net.Config;
using System.Reflection;

namespace FolderSync.Core
{
    public class FolderSynchronizer : ISyncFolder
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(FolderSynchronizer));

        private readonly string _sourceFolder;
        private readonly string _replicaFolder;
        private readonly int _syncIntervalMilliseconds;
        private readonly string _logFilePath;

        // Stores the last successfully synchronized LastWriteTime for each relative file path
        private Dictionary<string, DateTime> _fileLastModifiedTimes = new();

        public FolderSynchronizer(string sourceFolder, string replicaFolder, int syncInterval, string logPath)
        {
            try
            {
                var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
                XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

                (_sourceFolder, _replicaFolder, _syncIntervalMilliseconds, _logFilePath) =
                    ValidateAndNormalizeParameters(sourceFolder, replicaFolder, syncInterval, logPath);

                var fileAppender = logRepository.GetAppenders()
                    .OfType<log4net.Appender.FileAppender>()
                    .FirstOrDefault(a => a.Name == "FileAppender");
                if (fileAppender != null)
                {
                    fileAppender.File = _logFilePath;
                    fileAppender.ActivateOptions();
                }

                Logger.Info($"FolderSynchronizer initialized. Source='{_sourceFolder}', Replica='{_replicaFolder}', IntervalMs={_syncIntervalMilliseconds}, LogFile='{_logFilePath}'");
            }
            catch (Exception ex)
            {
                Logger.Error("Exception during FolderSynchronizer initialization", ex);
                throw;
            }
        }

        public void SyncFolder()
        {
            try
            {
                // Take a fresh snapshot of the source
                var currentSnapshot = GetAllFiles(_sourceFolder);

                // Copy / update changed or new files
                foreach (var kvp in currentSnapshot)
                {
                    var relativePath = kvp.Key;
                    var currentLastWrite = kvp.Value;

                    var sourceFilePath = Path.Combine(_sourceFolder, relativePath);
                    var replicaFilePath = Path.Combine(_replicaFolder, relativePath);

                    // Determine whether this file is new or modified
                    var shouldCopy = !_fileLastModifiedTimes.TryGetValue(relativePath, out var previousLastWrite)
                                     || previousLastWrite != currentLastWrite
                                     || !File.Exists(replicaFilePath); // safety fallback

                    if (!shouldCopy)
                        continue;

                    try
                    {
                        var replicaDir = Path.GetDirectoryName(replicaFilePath);
                        if (!string.IsNullOrEmpty(replicaDir) && !Directory.Exists(replicaDir))
                        {
                            Directory.CreateDirectory(replicaDir);
                            Logger.Debug($"Created replica subdirectory: '{replicaDir}'");
                        }

                        File.Copy(sourceFilePath, replicaFilePath, true);
                        if (!_fileLastModifiedTimes.ContainsKey(relativePath))
                            Logger.Info($"Copied NEW file '{sourceFilePath}' -> '{replicaFilePath}'.");
                        else
                            Logger.Info($"Updated file '{sourceFilePath}' -> '{replicaFilePath}'.");
                    }
                    catch (Exception fileEx)
                    {
                        Logger.Error($"Error copying/updating '{sourceFilePath}' to '{replicaFilePath}': {fileEx.Message}", fileEx);
                    }
                }

                // Remove extraneous files from replica (those not present in current snapshot)
                var replicaFiles = GetAllFiles(_replicaFolder);
                var toDelete = replicaFiles.Keys.Except(currentSnapshot.Keys).ToList();
                foreach (var relative in toDelete)
                {
                    try
                    {
                        var fullPath = Path.Combine(_replicaFolder, relative);
                        File.Delete(fullPath);
                        Logger.Info($"Deleted extraneous file '{fullPath}'.");
                    }
                    catch (Exception delEx)
                    {
                        Logger.Error($"Error deleting extraneous file '{relative}': {delEx.Message}", delEx);
                    }
                }

                // Prune empty directories
                try
                {
                    var dirs = Directory.GetDirectories(_replicaFolder, "*", SearchOption.AllDirectories)
                        .OrderByDescending(d => d.Length);
                    foreach (var dir in dirs)
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir);
                            Logger.Debug($"Removed empty directory '{dir}'.");
                        }
                    }
                }
                catch (Exception dirEx)
                {
                    Logger.Warn($"Error while pruning empty directories: {dirEx.Message}");
                }

                // Promote snapshot to become the "last synchronized" state
                _fileLastModifiedTimes = currentSnapshot;

                Logger.Info("Synchronization cycle completed.");
            }
            catch (Exception ex)
            {
                Logger.Error("Error during folder synchronization cycle", ex);
                throw;
            }
        }

        #region Private Helpers

        private Dictionary<string, DateTime> GetAllFiles(string folderPath)
        {
            var files = new Dictionary<string, DateTime>();
            foreach (var file in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(folderPath, file);
                // Using local time is fine here; if you need cross-timezone precision switch to GetLastWriteTimeUtc
                files[relativePath] = File.GetLastWriteTime(file);
            }
            return files;
        }

        private static (string source, string replica, int intervalMs, string logFile) ValidateAndNormalizeParameters(
            string sourceFolder,
            string replicaFolder,
            int syncInterval,
            string logPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceFolder, nameof(sourceFolder));
            ArgumentException.ThrowIfNullOrWhiteSpace(replicaFolder, nameof(replicaFolder));
            ArgumentException.ThrowIfNullOrWhiteSpace(logPath, nameof(logPath));
            if (syncInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(syncInterval), "Sync interval must be greater than zero (milliseconds).");

            if (!Directory.Exists(sourceFolder))
                throw new DirectoryNotFoundException($"Source folder '{sourceFolder}' does not exist.");

            if (!Directory.Exists(replicaFolder))
                Directory.CreateDirectory(replicaFolder);

            string finalLogFilePath;
            if (Directory.Exists(logPath) || !Path.HasExtension(logPath))
            {
                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }
                finalLogFilePath = Path.Combine(logPath, "file.log");
            }
            else
            {
                var dir = Path.GetDirectoryName(logPath);
                if (string.IsNullOrWhiteSpace(dir))
                    throw new ArgumentException("Log file path must include a directory.", nameof(logPath));
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                finalLogFilePath = logPath;
            }

            return (sourceFolder, replicaFolder, syncInterval, finalLogFilePath);
        }

        #endregion
    }
}
