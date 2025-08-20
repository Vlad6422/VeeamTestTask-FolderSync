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
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                    Console.WriteLine("Cancellation requested. Finishing current cycle...");
                };

                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        syncFolder.SyncFolder();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error during synchronization cycle: {ex.Message}");
                    }

                    // Break immediately if cancelled after cycle
                    if (cts.IsCancellationRequested) break;

                    try
                    {
                        Task.Delay(opts.syncInterval, cts.Token).Wait();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                Console.WriteLine("Folder synchronization stopped.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error during initialization: {ex.Message}");
            }
        }

        static void HandleErrors(IEnumerable<Error> errs)
        {
            Console.Error.WriteLine("Invalid arguments. Usage:");
            Console.Error.WriteLine("FolderSync.exe -s \"C:\\Source\" -r \"C:\\Replica\" -i 200 -l \"C:\\Logs\"");
        }
    }
}
