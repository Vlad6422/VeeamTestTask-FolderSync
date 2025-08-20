namespace FolderSync.Contracts
{
    public interface IFolderSnapshotProvider
    {
        Dictionary<string, DateTime> GetSnapshot(string folderPath);
    }
}