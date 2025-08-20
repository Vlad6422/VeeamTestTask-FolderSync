using FolderSync.Core;

namespace FolderSync.Tests.Unit
{
    [CollectionDefinition("FolderSyncSerial", DisableParallelization = true)]
    public class FolderSyncSerialCollection { }

    [Collection("FolderSyncSerial")]
    public class FolderSynchronizerTests : IDisposable
    {
        private readonly string _root;
        private readonly string _source;
        private readonly string _replica;
        private readonly string _logs;

        public FolderSynchronizerTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "FolderSyncTests_" + Guid.NewGuid());
            _source = Path.Combine(_root, "source");
            _replica = Path.Combine(_root, "replica");
            _logs = Path.Combine(_root, "logs");
            Directory.CreateDirectory(_source);
            Directory.CreateDirectory(_replica);
            Directory.CreateDirectory(_logs);
        }

        [Fact]
        public void SyncFolder_CopiesRootFilesAndOverwritesExisting()
        {
            var fileA = Path.Combine(_source, "A.txt");
            var fileB = Path.Combine(_source, "B.txt");
            File.WriteAllText(fileA, "Alpha");
            File.WriteAllText(fileB, "Bravo");

            var replicaA = Path.Combine(_replica, "A.txt");
            File.WriteAllText(replicaA, "OLD CONTENT");

            var sut = new FolderSynchronizer(_source, _replica, 1000, _logs);

            // Act
            sut.SyncFolder();

            // Assert
            Assert.True(File.Exists(replicaA), "A.txt should exist in replica.");
            Assert.True(File.Exists(Path.Combine(_replica, "B.txt")), "B.txt should exist in replica.");
            Assert.Equal("Alpha", File.ReadAllText(replicaA));
            Assert.Equal("Bravo", File.ReadAllText(Path.Combine(_replica, "B.txt")));
        }

        [Fact]
        public void SyncFolder_DoesNotThrow_WhenReplicaAlreadyHasExtraFiles()
        {
            // Arrange
            var sourceFile = Path.Combine(_source, "OnlyInSource.txt");
            File.WriteAllText(sourceFile, "Payload");
            var extraReplicaFile = Path.Combine(_replica, "OnlyInReplica.txt");
            File.WriteAllText(extraReplicaFile, "ShouldStay");

            var sut = new FolderSynchronizer(_source, _replica, 500, _logs);

            // Act
            Exception? ex = Record.Exception(() => sut.SyncFolder());

            // Assert
            Assert.Null(ex);
            Assert.True(File.Exists(Path.Combine(_replica, "OnlyInSource.txt")));
            // We do not delete extraneous files (current implementation), so ensure it still exists.
            Assert.True(File.Exists(extraReplicaFile));
        }

        [Fact]
        public void SyncFolder_CopiesNestedFiles_WhenTargetSubdirectoriesExist()
        {
            var nestedDir = Path.Combine(_source, "Sub1", "Sub2");
            Directory.CreateDirectory(nestedDir);
            var nestedFile = Path.Combine(nestedDir, "Deep.txt");
            File.WriteAllText(nestedFile, "DeepContent");

            Directory.CreateDirectory(Path.Combine(_replica, "Sub1", "Sub2"));

            var sut = new FolderSynchronizer(_source, _replica, 750, _logs);

            sut.SyncFolder();
            var replicated = Path.Combine(_replica, "Sub1", "Sub2", "Deep.txt");
            Assert.True(File.Exists(replicated), "Nested file should be copied when directory exists.");
            Assert.Equal("DeepContent", File.ReadAllText(replicated));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, recursive: true);
            }
            catch
            {
                // Ehhhh, best effort cleanup
            }
        }
    }
}