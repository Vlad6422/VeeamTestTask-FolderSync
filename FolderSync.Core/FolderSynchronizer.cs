using FolderSync.Contracts;
using log4net;
using log4net.Config;
using System.Reflection;

namespace FolderSync.Core
{
    public sealed class FolderSynchronizer : ISyncFolder
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(FolderSynchronizer));

        private readonly string _sourceFolder;
        private readonly string _replicaFolder;
        private readonly int _syncIntervalMilliseconds;
        private readonly string _logFilePath;

        private readonly IFolderSnapshotProvider _snapshotProvider;
        private readonly IFileCopyService _fileCopyService;
        private readonly IReplicaMaintenance _replicaMaintenance;

        private Dictionary<string, DateTime> _previousSnapshot = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _syncLock = new();

        #region Constructors / Factory

        // Primary DI-friendly constructor
        public FolderSynchronizer(
            FolderSyncOptions options,
            IFolderSnapshotProvider snapshotProvider,
            IFileCopyService fileCopyService,
            IReplicaMaintenance replicaMaintenance)
        {
            (_sourceFolder, _replicaFolder, _syncIntervalMilliseconds, _logFilePath) = options.NormalizeAndValidate();

            _snapshotProvider = snapshotProvider;
            _fileCopyService = fileCopyService;
            _replicaMaintenance = replicaMaintenance;

            ConfigureLogging();
            Logger.Info($"FolderSynchronizer initialized. Source='{_sourceFolder}', Replica='{_replicaFolder}', IntervalMs={_syncIntervalMilliseconds}, LogFile='{_logFilePath}'");
        }

        // Backwards-compatible convenience constructor
        public FolderSynchronizer(string sourceFolder, string replicaFolder, int syncInterval, string logPath)
            : this(new FolderSyncOptions(sourceFolder, replicaFolder, syncInterval, logPath),
                snapshotProvider: new FolderSnapshotProvider(),
                fileCopyService: new RetryingFileCopyService(),
                replicaMaintenance: new ReplicaMaintenance(new FolderSnapshotProvider()))
        {
        }

        private void ConfigureLogging()
        {
            try
            {
                var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
                XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

                var fileAppender = logRepository.GetAppenders()
                    .OfType<log4net.Appender.FileAppender>()
                    .FirstOrDefault(a => a.Name == "FileAppender");
                if (fileAppender != null)
                {
                    fileAppender.File = _logFilePath;
                    fileAppender.ActivateOptions();
                }
            }
            catch (Exception ex)
            {
                // Last resort logging
                Logger.Error("Exception during logging configuration", ex);
                throw;
            }
        }

        #endregion

        public void SyncFolder()
        {
            if (!Monitor.TryEnter(_syncLock))
            {
                Logger.Warn("Sync attempt skipped; previous sync still running.");
                return;
            }

            try
            {
                var currentSnapshot = _snapshotProvider.GetSnapshot(_sourceFolder);
                CopyOrUpdateFiles(currentSnapshot);
                _replicaMaintenance.DeleteExtraneousFiles(_sourceFolder, _replicaFolder, currentSnapshot);
                _replicaMaintenance.PruneEmptyDirectories(_replicaFolder);

                _previousSnapshot = currentSnapshot;
                Logger.Info("Synchronization cycle completed.");
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error during synchronization cycle", ex);
                throw;
            }
            finally
            {
                Monitor.Exit(_syncLock);
            }
        }

        #region Private Methods
        private void CopyOrUpdateFiles(Dictionary<string, DateTime> currentSnapshot)
        {
            foreach (var (relativePath, currentLastWriteUtc) in currentSnapshot)
            {
                var src = Path.Combine(_sourceFolder, relativePath);
                var dst = Path.Combine(_replicaFolder, relativePath);

                var needsCopy =
                    !_previousSnapshot.TryGetValue(relativePath, out var prevWriteUtc) ||
                    prevWriteUtc != currentLastWriteUtc ||
                    !File.Exists(dst);

                if (!needsCopy)
                    continue;

                try
                {
                    _fileCopyService.CopyFile(src, dst, overwrite: true, retries: 2, retryDelayMs: 50);

                    if (!_previousSnapshot.ContainsKey(relativePath))
                        Logger.Info($"Copied NEW file '{src}' -> '{dst}'.");
                    else
                        Logger.Info($"Updated file '{src}' -> '{dst}'.");
                }
                catch (FileNotFoundException)
                {
                    Logger.Warn($"Skipped copy (source vanished mid-operation): '{src}'.");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed copying '{src}' -> '{dst}': {ex.Message}", ex);
                }
            }
        }
        #endregion
    }
}
