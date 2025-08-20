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
        private readonly int _syncInterval;
        private readonly string _logFolderPath;

        private Dictionary<string, DateTime> _fileLastModifiedTimes = new Dictionary<string, DateTime>();

        public FolderSynchronizer(string sourceFolder, string replicaFolder, int syncInterval, string logFolderPath)
        {
            try
            {
                // Configure log4net (only once per app domain)
                var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
                XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

                // Set log file path dynamically for FileAppender
                var fileAppender = logRepository.GetAppenders()
                    .OfType<log4net.Appender.FileAppender>()
                    .FirstOrDefault(a => a.Name == "FileAppender");
                if (fileAppender != null)
                {
                    fileAppender.File = logFolderPath + "\\file.log";
                    fileAppender.ActivateOptions();
                }

                ExceptionsCheck(sourceFolder, replicaFolder, syncInterval, logFolderPath);

                _sourceFolder = sourceFolder;
                _replicaFolder = replicaFolder;
                _syncInterval = syncInterval;
                _logFolderPath = logFolderPath;

                Logger.Info("FolderSynchronizer initialized.");
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
                Logger.Info("Starting folder synchronization.");
                _fileLastModifiedTimes = GetAllFiles(_sourceFolder);
                foreach (var file in _fileLastModifiedTimes)
                {
                    var sourceFilePath = Path.Combine(_sourceFolder, file.Key);
                    var replicaFilePath = Path.Combine(_replicaFolder, file.Key);
                    try
                    {
                        File.Copy(sourceFilePath, replicaFilePath, true);
                        Logger.Info($"Copied '{sourceFilePath}' to '{replicaFilePath}'.");
                    }
                    catch (Exception fileEx)
                    {
                        Logger.Error($"Error copying '{sourceFilePath}' to '{replicaFilePath}': {fileEx.Message}", fileEx);
                    }
                }
                Logger.Info("Folder synchronization completed.");
            }
            catch (Exception ex)
            {
                Logger.Error("Error during folder synchronization", ex);
                throw;
            }
        }

        #region Private Methods

        private Dictionary<string, DateTime> GetAllFiles(string folderPath)
        {
            var files = new Dictionary<string, DateTime>();
            try
            {
                foreach (var file in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(folderPath, file);
                    files[relativePath] = File.GetLastWriteTime(file);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting files from folder '{folderPath}': {ex.Message}", ex);
                throw;
            }
            return files;
        }

        private void ExceptionsCheck(string sourceFolder, string replicaFolder, int syncInterval, string logFilePath)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(sourceFolder, nameof(sourceFolder));
                ArgumentException.ThrowIfNullOrWhiteSpace(replicaFolder, nameof(replicaFolder));
                ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath, nameof(logFilePath));
                if (syncInterval <= 0)
                    throw new ArgumentOutOfRangeException(nameof(syncInterval), "Sync interval must be greater than zero.");

                if (!Directory.Exists(sourceFolder))
                    throw new DirectoryNotFoundException($"Source folder '{sourceFolder}' does not exist.");

                // Replica folder: create if missing, don't throw
                if (!Directory.Exists(replicaFolder))
                    Directory.CreateDirectory(replicaFolder);

                var logDir = Path.GetDirectoryName(logFilePath);
                if (string.IsNullOrWhiteSpace(logDir))
                    throw new ArgumentException("Log file path must include a directory.", nameof(logFilePath));
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
            }
            catch (Exception ex)
            {
                Logger.Error("Exception during parameter validation", ex);
                throw;
            }
        }

        #endregion 
    }
}
