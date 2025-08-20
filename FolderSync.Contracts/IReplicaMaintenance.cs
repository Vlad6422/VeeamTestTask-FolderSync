namespace FolderSync.Contracts
{
    public interface IReplicaMaintenance
    {
        void DeleteExtraneousFiles(string sourceRoot, string replicaRoot, IReadOnlyDictionary<string, DateTime> currentSourceSnapshot);
        void PruneEmptyDirectories(string replicaRoot);
    }
}