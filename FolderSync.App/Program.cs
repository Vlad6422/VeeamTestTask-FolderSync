using FolderSync.App;

namespace FolderSync.App
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            return await SyncApp.RunAsync(args);
        }
    }
}
