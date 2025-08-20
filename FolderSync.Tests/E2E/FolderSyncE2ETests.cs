using System.Diagnostics;
using System.Text;

namespace FolderSync.Tests.E2E
{
    public class FolderSyncE2ETests : IDisposable
    {
        private readonly string _root;
        private readonly string _source;
        private readonly string _replica;
        private readonly string _logs;
        private readonly string _configuration;
        private readonly string _solutionDir;

        public FolderSyncE2ETests()
        {
            _root = Path.Combine(Path.GetTempPath(), "FolderSyncE2E_" + Guid.NewGuid());
            _source = Path.Combine(_root, "source");
            _replica = Path.Combine(_root, "replica");
            _logs = Path.Combine(_root, "logs");
            Directory.CreateDirectory(_source);
            Directory.CreateDirectory(_replica);
            Directory.CreateDirectory(_logs);

            var baseDir = AppContext.BaseDirectory;
            var netDir = new DirectoryInfo(baseDir);
            var configDir = netDir.Parent;
            _configuration = configDir!.Name;
            var binDir = configDir.Parent;
            var testProjDir = binDir!.Parent;
            _solutionDir = testProjDir!.Parent!.FullName;
        }

        private string GetAppExecutablePath()
        {
            var appOutDir = Path.Combine(_solutionDir, "FolderSync.App", "bin", _configuration, "net8.0");
            var exeName = OperatingSystem.IsWindows() ? "FolderSync.exe" : "FolderSync.App";
            var exePath = Path.Combine(appOutDir, exeName);
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"Expected application executable at '{exePath}'. Ensure the App project is built before running E2E tests.");
            }
            return exePath;
        }

        private Process StartApp(StringBuilder stdout, StringBuilder stderr, params string[] args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = GetAppExecutablePath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = string.Join(' ', args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))
            };

            var proc = Process.Start(psi)!;
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            return proc;
        }

        [Fact]
        public void OneWaySynchronization_ResultingReplicaExactlyMatchesSource_IncludingRemovals()
        {
            var nestedDir = Path.Combine(_source, "dir1", "dir2");
            Directory.CreateDirectory(nestedDir);
            File.WriteAllText(Path.Combine(_source, "fileA.txt"), "Alpha");
            File.WriteAllText(Path.Combine(_source, "fileB.txt"), "Bravo");
            File.WriteAllText(Path.Combine(nestedDir, "deep.txt"), "Deep");

            File.WriteAllText(Path.Combine(_replica, "fileA.txt"), "OLD");
            File.WriteAllText(Path.Combine(_replica, "extraneous.txt"), "REMOVE_ME");

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var proc = StartApp(stdout, stderr,
                "-s", _source,
                "-r", _replica,
                "-i", "200",
                "-l", _logs
            );

            // Allow at least one cycle (interval=200ms) plus margin
            Thread.Sleep(600);

            // Terminate the continuously running process after first cycle
            if (!proc.HasExited)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                try { proc.WaitForExit(2_000); } catch { }
            }

            Assert.True(File.Exists(Path.Combine(_logs, "file.log")), "Log file was not created.");
            var logContent = File.ReadAllText(Path.Combine(_logs, "file.log"));
            Assert.Contains("Copied", logContent);

            var sourceFiles = GetRelativeFileMap(_source);
            var replicaFiles = GetRelativeFileMap(_replica);

            Assert.Equal(sourceFiles.Keys.OrderBy(x => x), replicaFiles.Keys.OrderBy(x => x));
            foreach (var kv in sourceFiles)
            {
                Assert.True(replicaFiles.ContainsKey(kv.Key), $"Replica missing '{kv.Key}'");
                Assert.Equal(kv.Value, replicaFiles[kv.Key]);
            }

            // Ensure extraneous file removed
            Assert.False(File.Exists(Path.Combine(_replica, "extraneous.txt")));
        }

        [Fact]
        public void PeriodicSynchronization_AppliesChangesAcrossIntervals()
        {
            File.WriteAllText(Path.Combine(_source, "initial.txt"), "V1");

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            using var proc = StartApp(stdout, stderr,
                "-s", _source,
                "-r", _replica,
                "-i", "200",
                "-l", _logs
            );

            // Wait for first cycle
            Thread.Sleep(500);

            File.WriteAllText(Path.Combine(_source, "initial.txt"), "V2");
            File.WriteAllText(Path.Combine(_source, "added.txt"), "NEW");

            // Wait for second cycle
            Thread.Sleep(500);

            if (!proc.HasExited)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
            }

            var replicaFile = Path.Combine(_replica, "initial.txt");
            Assert.True(File.Exists(replicaFile), "Replica missing updated file after second interval.");
            Assert.Equal("V2", File.ReadAllText(replicaFile));
            Assert.True(File.Exists(Path.Combine(_replica, "added.txt")), "Replica missing newly added file after second interval.");
        }

        private static Dictionary<string, string> GetRelativeFileMap(string root)
        {
            return Directory
                .GetFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(f => Path.GetRelativePath(root, f).Replace('\\', '/'), f => File.ReadAllText(f));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, true);
            }
            catch { }
        }
    }
}
