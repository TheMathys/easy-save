using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Infrastructure.Persistence;

namespace EasySave.Infrastructure.Tests
{

    public class JsonConfigurationRepositoryTests
    {
        [Fact]
        public void Constructor_Throws_When_ConfigDirectory_Is_Null()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new JsonConfigurationRepository((string)null!));
            Assert.Equal("configDirectory", exception.ParamName);
        }

        [Fact]
        public async Task LoadAsync_ReturnsNull_When_File_Does_Not_Exist()
        {
            var tempDir = CreateUniqueTempDirectory();

            try
            {
                var repository = new JsonConfigurationRepository(tempDir);

                var result = await repository.LoadAsync();

                Assert.Null(result);
            }
            finally
            {
                DeleteDirectoryIfExists(tempDir);
            }
        }

        [Fact]
        public async Task SaveAsync_Then_LoadAsync_Roundtrips_Configuration()
        {
            var tempDir = CreateUniqueTempDirectory();

            try
            {
                var repository = new JsonConfigurationRepository(tempDir);

                var originalConfig = new BackupConfiguration
                {
                    LogAndStateDirectory = Path.Combine(tempDir, "logs"),
                    LogFileFormat = LogFileFormat.Xml,
                    Jobs = System.Array.AsReadOnly(new[]
                    {
                        new BackupJob
                        {
                            Id = 1,
                            Name = "Job1",
                            SourcePath = @"C:\Source1",
                            TargetPath = @"D:\Target1",
                            Type = BackupType.Full
                        },
                        new BackupJob
                        {
                            Id = 2,
                            Name = "Job2",
                            SourcePath = @"C:\Source2",
                            TargetPath = @"D:\Target2",
                            Type = BackupType.Differential
                        }
                    }),
                    LastFullBackupUtcByJobId = new Dictionary<int, DateTime>
                    {
                        [1] = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc)
                    },
                    LargeFileThresholdKb = 2048
                };

                await repository.SaveAsync(originalConfig);

                var loadedConfig = await repository.LoadAsync();

                Assert.NotNull(loadedConfig);
                Assert.Equal(originalConfig.LogAndStateDirectory, loadedConfig!.LogAndStateDirectory);
                Assert.Equal(originalConfig.LogFileFormat, loadedConfig.LogFileFormat);
                Assert.Equal(originalConfig.LargeFileThresholdKb, loadedConfig.LargeFileThresholdKb);

                Assert.Equal(originalConfig.Jobs.Count, loadedConfig.Jobs.Count);
                for (var i = 0; i < originalConfig.Jobs.Count; i++)
                {
                    var expected = originalConfig.Jobs[i];
                    var actual = loadedConfig.Jobs[i];

                    Assert.Equal(expected.Id, actual.Id);
                    Assert.Equal(expected.Name, actual.Name);
                    Assert.Equal(expected.SourcePath, actual.SourcePath);
                    Assert.Equal(expected.TargetPath, actual.TargetPath);
                    Assert.Equal(expected.Type, actual.Type);
                }

                Assert.Equal(
                    originalConfig.LastFullBackupUtcByJobId.Count,
                    loadedConfig.LastFullBackupUtcByJobId.Count);

                foreach (var kvp in originalConfig.LastFullBackupUtcByJobId)
                {
                    Assert.True(loadedConfig.LastFullBackupUtcByJobId.TryGetValue(kvp.Key, out var actualValue));
                    Assert.Equal(kvp.Value, actualValue);
                }
            }
            finally
            {
                DeleteDirectoryIfExists(tempDir);
            }
        }

        [Fact]
        public async Task UpdateLastFullBackupAsync_Updates_Dictionary_And_Persists()
        {
            var tempDir = CreateUniqueTempDirectory();

            try
            {
                var repository = new JsonConfigurationRepository(tempDir);

                var initialConfig = new BackupConfiguration
                {
                    LogAndStateDirectory = Path.Combine(tempDir, "logs"),
                    Jobs = new List<BackupJob>
                {
                    new()
                    {
                        Id = 1,
                        Name = "Job1",
                        SourcePath = @"C:\Source1",
                        TargetPath = @"D:\Target1",
                        Type = BackupType.Full
                    }
                },
                    LastFullBackupUtcByJobId = new Dictionary<int, DateTime>()
                };

                await repository.SaveAsync(initialConfig);

                var newUtc = new DateTime(2024, 2, 1, 8, 30, 0, DateTimeKind.Utc);
                await repository.UpdateLastFullBackupAsync(1, newUtc);

                var updatedConfig = await repository.LoadAsync();

                Assert.NotNull(updatedConfig);
                Assert.True(updatedConfig!.LastFullBackupUtcByJobId.ContainsKey(1));
                Assert.Equal(newUtc, updatedConfig.LastFullBackupUtcByJobId[1]);
            }
            finally
            {
                DeleteDirectoryIfExists(tempDir);
            }
        }

        private static string CreateUniqueTempDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "EasySave.Infrastructure.Tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (!Directory.Exists(path))
                return;

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests.
            }
        }
    }
}