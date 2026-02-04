using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using EasySave.Core.Entities;
using EasySave.Infrastructure.Persistence;

namespace EasySave.Tests.Persistence
{
    public class JsonStateWriterTests : IDisposable
    {
        private readonly List<string> _filesToDelete = new List<string>();

        private string GetTempFilePath()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "state.json");
            _filesToDelete.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (var file in _filesToDelete)
            {
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                    var dir = Path.GetDirectoryName(file);
                    if (Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0) Directory.Delete(dir);
                }
                catch { }
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_ShouldThrowArgumentNullException_WhenPathIsInvalid(string? invalidPath)
        {
            Assert.Throws<ArgumentNullException>(() => new JsonStateWriter(invalidPath!));
        }

        [Fact]
        public async Task WriteStateAsync_ShouldCreateFile_WhenCalled()
        {
            // Arrange
            var filePath = GetTempFilePath();
            var writer = new JsonStateWriter(filePath);


            var progressList = new List<BackupProgress>
            {
                new BackupProgress(), 
                new BackupProgress()  
            };

            // Act
            await writer.WriteStateAsync(progressList);

            // Assert
            Assert.True(File.Exists(filePath), "Le fichier doit être créé sur le disque.");

            string content = await File.ReadAllTextAsync(filePath);

         
            Assert.NotEmpty(content);
            Assert.Contains("[", content);
            Assert.Contains("]", content);
        }

        [Fact]
        public async Task WriteStateAsync_ShouldCreateDirectory_IfItDoesNotExist()
        {
            // Arrange
            var deepPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "SubFolder", "state.json");
            _filesToDelete.Add(deepPath);

            var writer = new JsonStateWriter(deepPath);

            // Act
            await writer.WriteStateAsync(new List<BackupProgress>());

            // Assert
            Assert.True(File.Exists(deepPath), "Le dossier parent aurait dû être créé.");
        }
    }
}