namespace VeeamTestTask_FolderSync.Contracts
{
    interface ISyncFolder
    {
        public void SyncFolder(string sourcePath, string destinationPath, TimeSpan syncInterval);

    }
}
