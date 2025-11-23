using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using McpMemoryServer.Services;
using Xunit;

namespace McpMemoryServer.Tests.Services
{
    public class FileSystemServiceTests : IDisposable
    {
        private readonly FileSystemService _fileSystemService;
        private readonly Mock<ILogger<FileSystemService>> _loggerMock;
        private readonly string _testBaseDirectory;

        public FileSystemServiceTests()
        {
            _testBaseDirectory = Path.Combine(Path.GetTempPath(), $"mcp_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testBaseDirectory);

            var configDict = new Dictionary<string, string>
            {
                ["FileSystem:BaseDirectory"] = _testBaseDirectory,
                ["FileSystem:AllowedExtensions"] = ".txt,.md,.json,.log"
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configDict!)
                .Build();

            _loggerMock = new Mock<ILogger<FileSystemService>>();
            _fileSystemService = new FileSystemService(configuration, _loggerMock.Object);
        }

        [Fact]
        public async Task ReadFileAsync_ExistingFile_ReturnsContent()
        {
            // Arrange
            var fileName = "test.txt";
            var content = "Test file content";
            var filePath = Path.Combine(_testBaseDirectory, fileName);
            await File.WriteAllTextAsync(filePath, content);

            // Act
            var result = await _fileSystemService.ReadFileAsync(fileName);

            // Assert
            Assert.Equal(content, result);
        }

        [Fact]
        public async Task ReadFileAsync_NonExistentFile_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _fileSystemService.ReadFileAsync("nonexistent.txt"));
        }

        [Fact]
        public async Task ReadFileAsync_DisallowedExtension_ThrowsException()
        {
            // Arrange
            var fileName = "test.exe";
            var filePath = Path.Combine(_testBaseDirectory, fileName);
            await File.WriteAllTextAsync(filePath, "content");

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _fileSystemService.ReadFileAsync(fileName));
        }

        [Fact]
        public async Task ReadFileAsync_PathTraversal_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _fileSystemService.ReadFileAsync("../../../etc/passwd"));
        }

        [Fact]
        public async Task ListDirectoryAsync_ReturnsFilesAndDirectories()
        {
            // Arrange
            Directory.CreateDirectory(Path.Combine(_testBaseDirectory, "subdir"));
            await File.WriteAllTextAsync(Path.Combine(_testBaseDirectory, "file1.txt"), "content");
            await File.WriteAllTextAsync(Path.Combine(_testBaseDirectory, "file2.md"), "content");

            // Act
            var items = await _fileSystemService.ListDirectoryAsync(".");

            // Assert
            var itemList = items.ToList();
            Assert.Contains(itemList, i => i.Name == "subdir" && i.IsDirectory);
            Assert.Contains(itemList, i => i.Name == "file1.txt" && !i.IsDirectory);
            Assert.Contains(itemList, i => i.Name == "file2.md" && !i.IsDirectory);
        }

        [Fact]
        public async Task ListDirectoryAsync_Recursive_ReturnsAllItems()
        {
            // Arrange
            var subdir = Path.Combine(_testBaseDirectory, "subdir");
            Directory.CreateDirectory(subdir);
            await File.WriteAllTextAsync(Path.Combine(_testBaseDirectory, "root.txt"), "content");
            await File.WriteAllTextAsync(Path.Combine(subdir, "nested.txt"), "content");

            // Act
            var items = await _fileSystemService.ListDirectoryAsync(".", recursive: true);

            // Assert
            var itemList = items.ToList();
            Assert.Contains(itemList, i => i.Name == "root.txt");
            Assert.Contains(itemList, i => i.Name == "nested.txt");
        }

        [Fact]
        public async Task ListDirectoryAsync_FiltersByAllowedExtensions()
        {
            // Arrange
            await File.WriteAllTextAsync(Path.Combine(_testBaseDirectory, "allowed.txt"), "content");
            await File.WriteAllTextAsync(Path.Combine(_testBaseDirectory, "blocked.exe"), "content");

            // Act
            var items = await _fileSystemService.ListDirectoryAsync(".");

            // Assert
            var itemList = items.ToList();
            Assert.Contains(itemList, i => i.Name == "allowed.txt");
            Assert.DoesNotContain(itemList, i => i.Name == "blocked.exe");
        }

        [Fact]
        public async Task ListDirectoryAsync_NonExistentDirectory_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
                await _fileSystemService.ListDirectoryAsync("nonexistent"));
        }

        [Fact]
        public async Task SearchFilesAsync_FindsMatchingFiles()
        {
            // Arrange
            await File.WriteAllTextAsync(Path.Combine(_testBaseDirectory, "test1.txt"), "content");
            await File.WriteAllTextAsync(Path.Combine(_testBaseDirectory, "test2.txt"), "content");
            await File.WriteAllTextAsync(Path.Combine(_testBaseDirectory, "other.md"), "content");

            // Act
            var files = await _fileSystemService.SearchFilesAsync("test*.txt");

            // Assert
            var fileList = files.ToList();
            Assert.Equal(2, fileList.Count);
            Assert.All(fileList, f => Assert.StartsWith("test", f.Name));
        }

        [Fact]
        public async Task SearchFilesAsync_RespectsAllowedExtensions()
        {
            // Arrange
            await File.WriteAllTextAsync(Path.Combine(_testBaseDirectory, "file.txt"), "content");
            await File.WriteAllTextAsync(Path.Combine(_testBaseDirectory, "file.exe"), "content");

            // Act
            var files = await _fileSystemService.SearchFilesAsync("file.*");

            // Assert
            var fileList = files.ToList();
            Assert.Single(fileList);
            Assert.Equal("file.txt", fileList[0].Name);
        }

        [Fact]
        public async Task FileExistsAsync_ExistingFile_ReturnsTrue()
        {
            // Arrange
            var fileName = "exists.txt";
            await File.WriteAllTextAsync(Path.Combine(_testBaseDirectory, fileName), "content");

            // Act
            var exists = await _fileSystemService.FileExistsAsync(fileName);

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task FileExistsAsync_NonExistentFile_ReturnsFalse()
        {
            // Act
            var exists = await _fileSystemService.FileExistsAsync("nonexistent.txt");

            // Assert
            Assert.False(exists);
        }

        [Fact]
        public async Task DirectoryExistsAsync_ExistingDirectory_ReturnsTrue()
        {
            // Arrange
            var dirName = "testdir";
            Directory.CreateDirectory(Path.Combine(_testBaseDirectory, dirName));

            // Act
            var exists = await _fileSystemService.DirectoryExistsAsync(dirName);

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task DirectoryExistsAsync_NonExistentDirectory_ReturnsFalse()
        {
            // Act
            var exists = await _fileSystemService.DirectoryExistsAsync("nonexistent");

            // Assert
            Assert.False(exists);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testBaseDirectory))
            {
                Directory.Delete(_testBaseDirectory, recursive: true);
            }
        }
    }
}
