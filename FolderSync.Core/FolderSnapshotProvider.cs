using FolderSync.Contracts;

namespace FolderSync.Core
{
    internal sealed class FolderSnapshotProvider : IFolderSnapshotProvider
    {
        public Dictionary<string, DateTime> GetSnapshot(string folderPath)
        {
            var dict = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(folderPath, file);
                dict[relative] = File.GetLastWriteTimeUtc(file); // UTC for consistency
            }
            return dict;
        }
    }
}