using CommandLine;

namespace VeeamTestTask_FolderSync
{
    internal class ConsoleArguments
    {
        [Option('s', "source", Required = true, HelpText = "Path to source folder.")]
        public required string sourcePath { get; set; }

        [Option('r', "replica", Required = true, HelpText = "Path to replica folder.")]
        public required string replicaFolder { get; set; }
        [Option('i', "interval", Required = true, HelpText = "Synchronization interval in minutes.")]
        public required TimeSpan syncInterval { get; set; }
        [Option('l', "log", Required = true, HelpText = "Path to log file.")]
        public required string log { get; set; }

    }
}
