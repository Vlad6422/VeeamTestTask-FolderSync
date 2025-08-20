using FolderSync.Contracts;
using log4net;
using log4net.Config;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

        // Snapshot persistence
        private readonly string _snapshotStatePath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Deferred load control
        private bool _snapshotLoaded;

        #region Constructors / Factory

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

            _snapshotStatePath = BuildSnapshotPath(_replicaFolder, _sourceFolder);

            ConfigureLogging();
            Logger.Info($"FolderSynchronizer initialized. Source='{_sourceFolder}', Replica='{_replicaFolder}', StateFile='{_snapshotStatePath}'");
        }

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
                var entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly == null)
                    throw new InvalidOperationException("Entry assembly is null. Cannot configure logging repository.");

                var logRepository = LogManager.GetRepository(entryAssembly);
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
                EnsurePreviousSnapshotLoaded();

                // Keep reference to old snapshot for change detection.
                var oldSnapshot = _previousSnapshot;

                var currentSnapshot = _snapshotProvider.GetSnapshot(_sourceFolder);
                CopyOrUpdateFiles(currentSnapshot);
                _replicaMaintenance.DeleteExtraneousFiles(_sourceFolder, _replicaFolder, currentSnapshot);
                _replicaMaintenance.PruneEmptyDirectories(_replicaFolder);

                bool changed = !SnapshotsEqual(oldSnapshot, currentSnapshot);

                _previousSnapshot = currentSnapshot;

                if (changed)
                {
                    SaveSnapshot();
                    Logger.Info("Synchronization cycle completed (changes persisted).");
                }
                else
                {
                    Logger.Debug("No changes detected; snapshot persistence skipped.");
                    Logger.Info("Synchronization cycle completed (no changes).");
                }
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

        private void EnsurePreviousSnapshotLoaded()
        {
            if (_snapshotLoaded)
                return;

            LoadPreviousSnapshot();
            _snapshotLoaded = true;
        }

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

        private static bool SnapshotsEqual(Dictionary<string, DateTime> a, Dictionary<string, DateTime> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.Count != b.Count) return false;

            foreach (var kv in a)
            {
                if (!b.TryGetValue(kv.Key, out var dt) || dt != kv.Value)
                    return false;
            }
            return true;
        }

        private static string BuildSnapshotPath(string replicaRoot, string sourceRoot)
        {
            var fullSource = Path.GetFullPath(sourceRoot);
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(fullSource));
            var hash = Convert.ToHexString(hashBytes, 0, 16);
            return Path.Combine(replicaRoot, $".foldersync.{hash}.snapshot.json");
        }

        private void LoadPreviousSnapshot()
        {
            try
            {
                if (!File.Exists(_snapshotStatePath))
                {
                    Logger.Debug("No previous snapshot state file found; starting fresh.");
                    return;
                }

                var json = File.ReadAllText(_snapshotStatePath);
                var state = JsonSerializer.Deserialize<SnapshotState>(json, _jsonOptions);
                if (state?.Files == null)
                {
                    Logger.Warn("Snapshot state file present but empty or malformed; starting fresh.");
                    return;
                }

                _previousSnapshot = state.Files
                    .Where(kv => kv.Value > 0)
                    .ToDictionary(
                        kv => kv.Key,
                        kv => DateTime.SpecifyKind(new DateTime(kv.Value, DateTimeKind.Utc), DateTimeKind.Utc),
                        StringComparer.OrdinalIgnoreCase);

                Logger.Info($"Loaded previous snapshot state ({_previousSnapshot.Count} entries).");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to load previous snapshot state: {ex.Message}. Starting fresh.");
                _previousSnapshot = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void SaveSnapshot()
        {
            try
            {
                var state = new SnapshotState
                {
                    Files = _previousSnapshot.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value.ToUniversalTime().Ticks,
                        StringComparer.OrdinalIgnoreCase)
                };

                var json = JsonSerializer.Serialize(state, _jsonOptions);

                var tmp = _snapshotStatePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, _snapshotStatePath, overwrite: true);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to persist snapshot state: {ex.Message}");
            }
        }

        private sealed class SnapshotState
        {
            public Dictionary<string, long> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        #endregion
    }
}
