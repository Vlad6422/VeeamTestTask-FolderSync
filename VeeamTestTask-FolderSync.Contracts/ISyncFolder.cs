namespace VeeamTestTask_FolderSync.Contracts
{
    public interface ISyncFolder
    {
        public void SyncFolder(string sourcePath, string destinationPath, TimeSpan syncInterval);

    }
}
