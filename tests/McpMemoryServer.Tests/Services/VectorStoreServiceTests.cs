using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using McpMemoryServer.Services;
using Xunit;

namespace McpMemoryServer.Tests.Services
{
    public class VectorStoreServiceTests : IAsyncLifetime
    {
        private readonly VectorStoreService _vectorStore;
        private readonly Mock<ILogger<VectorStoreService>> _loggerMock;
        private readonly string _testDbPath;

        public VectorStoreServiceTests()
        {
            _testDbPath = $"test_vectors_{Guid.NewGuid():N}.db";

            var configDict = new Dictionary<string, string>
            {
                ["ConnectionStrings:VectorStore"] = $"Data Source={_testDbPath}"
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configDict!)
                .Build();

            _loggerMock = new Mock<ILogger<VectorStoreService>>();
            _vectorStore = new VectorStoreService(configuration, _loggerMock.Object);
        }

        public async Task InitializeAsync()
        {
            await _vectorStore.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
            if (File.Exists($"{_testDbPath}-shm"))
            {
                File.Delete($"{_testDbPath}-shm");
            }
            if (File.Exists($"{_testDbPath}-wal"))
            {
                File.Delete($"{_testDbPath}-wal");
            }
            await Task.CompletedTask;
        }

        [Fact]
        public async Task AddTextAsync_ValidData_AddsText()
        {
            // Arrange
            var id = "test1";
            var content = "This is a test document about programming";
            var type = "document";

            // Act
            await _vectorStore.AddTextAsync(id, content, type);
            var count = await _vectorStore.GetCountAsync();

            // Assert
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task AddTextAsync_WithMetadata_StoresMetadata()
        {
            // Arrange
            var id = "test2";
            var content = "Document with metadata";
            var type = "document";
            var metadata = new Dictionary<string, object>
            {
                ["author"] = "Test Author",
                ["created_at"] = DateTime.UtcNow.ToString("O")
            };

            // Act
            await _vectorStore.AddTextAsync(id, content, type, metadata);

            // Assert - verify it was added
            var count = await _vectorStore.GetCountAsync(type);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task SearchAsync_FindsRelevantContent()
        {
            // Arrange
            await _vectorStore.AddTextAsync("doc1", "Python programming language tutorial", "document");
            await _vectorStore.AddTextAsync("doc2", "JavaScript web development guide", "document");
            await _vectorStore.AddTextAsync("doc3", "Python data science libraries", "document");

            // Act
            var results = await _vectorStore.SearchAsync("Python programming");

            // Assert
            Assert.NotEmpty(results);
            var resultList = results.ToList();
            Assert.Contains(resultList, r => r.Id == "doc1");
        }

        [Fact]
        public async Task SearchAsync_RespectsTopK()
        {
            // Arrange
            for (int i = 0; i < 20; i++)
            {
                await _vectorStore.AddTextAsync($"doc{i}", $"Document number {i} about testing", "document");
            }

            // Act
            var results = await _vectorStore.SearchAsync("testing", topK: 5);

            // Assert
            Assert.Equal(5, results.Count());
        }

        [Fact]
        public async Task SearchAsync_FiltersByType()
        {
            // Arrange
            await _vectorStore.AddTextAsync("fact1", "Python is a programming language", "fact");
            await _vectorStore.AddTextAsync("msg1", "I love Python programming", "message");
            await _vectorStore.AddTextAsync("fact2", "Python uses indentation", "fact");

            // Act
            var results = await _vectorStore.SearchAsync("Python", type: "fact");

            // Assert
            Assert.All(results, r => Assert.Equal("fact", r.Type));
        }

        [Fact]
        public async Task SearchAsync_NoResults_ReturnsEmpty()
        {
            // Arrange
            await _vectorStore.AddTextAsync("doc1", "Python programming", "document");

            // Act
            var results = await _vectorStore.SearchAsync("quantum physics");

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task DeleteAsync_ExistingId_ReturnsTrue()
        {
            // Arrange
            var id = "delete-test";
            await _vectorStore.AddTextAsync(id, "Content to delete", "test");

            // Act
            var result = await _vectorStore.DeleteAsync(id);

            // Assert
            Assert.True(result);
            var count = await _vectorStore.GetCountAsync();
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task DeleteAsync_NonExistentId_ReturnsFalse()
        {
            // Act
            var result = await _vectorStore.DeleteAsync("nonexistent");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetCountAsync_ReturnsCorrectCount()
        {
            // Arrange
            await _vectorStore.AddTextAsync("test1", "Content 1", "type1");
            await _vectorStore.AddTextAsync("test2", "Content 2", "type1");
            await _vectorStore.AddTextAsync("test3", "Content 3", "type2");

            // Act
            var totalCount = await _vectorStore.GetCountAsync();
            var type1Count = await _vectorStore.GetCountAsync("type1");

            // Assert
            Assert.Equal(3, totalCount);
            Assert.Equal(2, type1Count);
        }

        [Fact]
        public async Task AddTextAsync_DuplicateId_Replaces()
        {
            // Arrange
            var id = "duplicate-test";
            await _vectorStore.AddTextAsync(id, "Original content", "test");

            // Act
            await _vectorStore.AddTextAsync(id, "Updated content", "test");

            // Assert
            var count = await _vectorStore.GetCountAsync();
            Assert.Equal(1, count); // Should still be 1, not 2
        }
    }
}
