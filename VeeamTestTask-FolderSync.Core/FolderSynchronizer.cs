using VeeamTestTask_FolderSync.Contracts;
namespace VeeamTestTask_FolderSync.Core
{
    public class FolderSynchronizer : ISyncFolder
    {
        private readonly string _sourceFolder;
        private readonly string _replicaFolder;
        private readonly int _syncInterval;
        private readonly string _logFilePath;

        private Dictionary<string, DateTime> _fileLastModifiedTimes = new Dictionary<string, DateTime>();

        public FolderSynchronizer(string sourceFolder, string replicaFolder, int syncInterval, string logFilePath)
        {
            _sourceFolder = sourceFolder;
            _replicaFolder = replicaFolder;
            _syncInterval = syncInterval;
            _logFilePath = logFilePath;

        }
        public void SyncFolder(string sourcePath, string destinationPath, int syncInterval)
        {

        }
    }
}
