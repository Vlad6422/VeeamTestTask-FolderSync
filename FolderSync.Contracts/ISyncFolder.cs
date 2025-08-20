namespace FolderSync.Contracts
{
    public interface ISyncFolder
    {
        public void SyncFolder(string sourcePath, string destinationPath, int syncInterval);

    }
}
