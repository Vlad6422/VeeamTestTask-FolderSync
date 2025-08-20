using CommandLine;
using FolderSync.Core;
using FolderSync.App.ArgumentsParser;

namespace FolderSync.App;

internal static class SyncApp
{
    public static async Task<int> RunAsync(string[] args)
    {
        return await Parser.Default
            .ParseArguments<ConsoleArguments>(args)
            .MapResult(
                RunWithOptionsAsync,
                ShowUsageAsync);
    }

    private static async Task<int> RunWithOptionsAsync(ConsoleArguments opts)
    {
        if (opts.syncInterval <= 0)
        {
            Console.Error.WriteLine("Synchronization interval must be a positive integer (milliseconds).");
            return 2;
        }

        try
        {
            var synchronizer = new FolderSynchronizer(
                opts.sourcePath,
                opts.replicaFolder,
                opts.syncInterval,
                opts.log);

            using var cts = new CancellationTokenSource();
            ConsoleCancelEventHandler? handler = (s, e) =>
            {
                e.Cancel = true;
                if (!cts.IsCancellationRequested)
                {
                    Console.WriteLine("Cancellation requested. Finishing current cycle...");
                    cts.Cancel();
                }
            };
            Console.CancelKeyPress += handler;

            try
            {
                // First run immediately
                RunSingleCycle(synchronizer);

                // Use PeriodicTimer for cleaner interval handling
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(opts.syncInterval));

                while (!cts.IsCancellationRequested && await SafeWaitForNextTickAsync(timer, cts.Token))
                {
                    if (cts.IsCancellationRequested) break;
                    RunSingleCycle(synchronizer);
                }
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }

            Console.WriteLine("Folder synchronization stopped.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error during initialization: {ex.Message}");
            return 1;
        }
    }

    private static void RunSingleCycle(FolderSynchronizer synchronizer)
    {
        try
        {
            synchronizer.SyncFolder();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during synchronization cycle: {ex.Message}");
        }
    }

    private static async Task<bool> SafeWaitForNextTickAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static Task<int> ShowUsageAsync(IEnumerable<Error> _)
    {
        Console.Error.WriteLine("Invalid arguments.");
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  FolderSync.exe -s \"C:\\Source\" -r \"C:\\Replica\" -i 2000 -l \"C:\\Logs\\sync.log\"");
        return Task.FromResult(1);
    }
}
