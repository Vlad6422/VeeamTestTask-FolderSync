using CommandLine;
using FolderSync.Core;
using FolderSync.App.AgrumentsParser;

namespace FolderSync.App
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<ConsoleArguments>(args)
            .WithParsed(RunWithOptions)
            .WithNotParsed(HandleErrors);
        }

        static void RunWithOptions(ConsoleArguments opts)
        {
            Console.WriteLine($"Source path   : {opts.sourcePath}");
            Console.WriteLine($"Replica path  : {opts.replicaFolder}");
            Console.WriteLine($"Sync interval : {opts.syncInterval} miliseconds");
            Console.WriteLine($"Log file path : {opts.log}");

            var syncFolder = new FolderSynchronizer(opts.sourcePath, opts.replicaFolder, opts.syncInterval, opts.log);
        }

        static void HandleErrors(IEnumerable<Error> errs)
        {
            Console.Error.WriteLine("Invalid arguments. Usage:");
            Console.Error.WriteLine("FolderSync.exe -s \"C:\\Source\" -r \"C:\\SyncFolder\" -i 2000 -l \"C:\\Logs\"");
        }
    }
}
