using CommandLine;
using FolderSync.Core;
using FolderSync.App.ArgumentsParser;

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
            try
            {
                var syncFolder = new FolderSynchronizer(opts.sourcePath, opts.replicaFolder, opts.syncInterval, opts.log);
                try
                {
                    syncFolder.SyncFolder();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error during synchronization: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error during initialization: {ex.Message}");
                return;

            }

        }


        static void HandleErrors(IEnumerable<Error> errs)
        {
            Console.Error.WriteLine("Invalid arguments. Usage:");
            Console.Error.WriteLine("FolderSync.exe -s \"C:\\Source\" -r \"C:\\SyncFolder\" -i 2000 -l \"C:\\Logs\"");
        }
    }
}
