namespace VeeamTestTask_FolderSync.Core
{
    public class FolderSynchronizer
    {
        private readonly string _sourceFolder;
        private readonly string _replicaFolder;
        private readonly TimeSpan _syncInterval;
        private readonly string _logFilePath;

        public FolderSynchronizer(string sourceFolder, string replicaFolder, TimeSpan syncInterval, string logFilePath)
        {
            _sourceFolder = sourceFolder;
            _replicaFolder = replicaFolder;
            _syncInterval = syncInterval;
            _logFilePath = logFilePath;
        }
    }
}
