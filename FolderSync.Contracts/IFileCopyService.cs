namespace FolderSync.Contracts
{
    public interface IFileCopyService
    {
        void CopyFile(string sourceFilePath, string destinationFilePath, bool overwrite, int retries = 0, int retryDelayMs = 0);
    }
}