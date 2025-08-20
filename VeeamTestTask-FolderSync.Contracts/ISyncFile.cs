namespace VeeamTestTask_FolderSync.Contracts
{
    interface ISyncFile
    {
        void CreateFile(string path, string content);
        void DeleteFile(string path);
        void CopyFile(string sourcePath, string destinationPath);
        void MoveFile(string sourcePath, string destinationPath);
        void UpdateFile(string path, string content);
        bool FileExists(string path);
    }
}
