using FolderSync.Contracts;

namespace FolderSync.Core
{
    public sealed class RetryingFileCopyService : IFileCopyService
    {
        public void CopyFile(string sourceFilePath, string destinationFilePath, bool overwrite, int retries = 0, int retryDelayMs = 0)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (!File.Exists(sourceFilePath))
                        throw new FileNotFoundException("Source file missing during copy.", sourceFilePath);

                    var dir = Path.GetDirectoryName(destinationFilePath);
                    if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    File.Copy(sourceFilePath, destinationFilePath, overwrite);
                    return;
                }
                catch (IOException) when (attempt < retries)
                {
                    Thread.Sleep(retryDelayMs);
                }
            }
        }
    }
}