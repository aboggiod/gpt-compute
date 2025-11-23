using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using McpMemoryServer.Data;
using McpMemoryServer.Mcp.Models;
using McpMemoryServer.Services;
using System.Text.Json;
using Xunit;

namespace McpMemoryServer.Tests.Services
{
    public class McpServiceTests : IAsyncLifetime
    {
        private readonly Mock<IMemoryService> _memoryServiceMock;
        private readonly Mock<IVectorStoreService> _vectorStoreMock;
        private readonly Mock<IFileSystemService> _fileSystemMock;
        private readonly Mock<ISessionManager> _sessionManagerMock;
        private readonly Mock<ILogger<McpService>> _loggerMock;
        private readonly McpService _mcpService;

        public McpServiceTests()
        {
            _memoryServiceMock = new Mock<IMemoryService>();
            _vectorStoreMock = new Mock<IVectorStoreService>();
            _fileSystemMock = new Mock<IFileSystemService>();
            _sessionManagerMock = new Mock<ISessionManager>();
            _loggerMock = new Mock<ILogger<McpService>>();

            _mcpService = new McpService(
                _memoryServiceMock.Object,
                _vectorStoreMock.Object,
                _fileSystemMock.Object,
                _sessionManagerMock.Object,
                _loggerMock.Object);
        }

        public Task InitializeAsync() => Task.CompletedTask;
        public Task DisposeAsync() => Task.CompletedTask;

        #region Initialize Tests

        [Fact]
        public async Task HandleRequestAsync_Initialize_ReturnsSessionId()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 1,
                Method = "initialize",
                Params = new InitializeParams
                {
                    ProtocolVersion = "2024-11-05",
                    ClientInfo = new Implementation { Name = "TestClient", Version = "1.0" }
                }
            };

            _sessionManagerMock
                .Setup(m => m.CreateSessionAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new McpMemoryServer.Models.Session
                {
                    Id = 1,
                    SessionId = "test-session-id",
                    CreatedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                });

            // Act
            var response = await _mcpService.HandleRequestAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(request.Id, response.Id);
            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        #endregion

        #region Tools List Tests

        [Fact]
        public async Task HandleRequestAsync_ToolsList_ReturnsAllTools()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 2,
                Method = "tools/list",
                Params = new { }
            };

            // Act
            var response = await _mcpService.HandleRequestAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Null(response.Error);
            var result = JsonSerializer.Deserialize<ListToolsResult>(
                JsonSerializer.Serialize(response.Result));
            Assert.NotNull(result);
            Assert.NotEmpty(result.Tools);
            Assert.Contains(result.Tools, t => t.Name == "memory.read");
            Assert.Contains(result.Tools, t => t.Name == "memory.write");
            Assert.Contains(result.Tools, t => t.Name == "memory.search");
            Assert.Contains(result.Tools, t => t.Name == "filesystem.read_file");
        }

        [Fact]
        public void GetAvailableTools_ReturnsCorrectToolCount()
        {
            // Act
            var tools = _mcpService.GetAvailableTools();

            // Assert
            Assert.Equal(6, tools.Count);
        }

        #endregion

        #region Tool Call - Memory.Read Tests

        [Fact]
        public async Task HandleRequestAsync_MemoryRead_ValidSession_ReturnsData()
        {
            // Arrange
            var sessionId = "test-session";
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 3,
                Method = "tools/call",
                Params = new CallToolParams
                {
                    Name = "memory.read",
                    Arguments = new Dictionary<string, object>
                    {
                        ["type"] = "facts",
                        ["session_id"] = sessionId
                    }
                }
            };

            _sessionManagerMock
                .Setup(m => m.IsSessionValidAsync(sessionId))
                .ReturnsAsync(true);

            _memoryServiceMock
                .Setup(m => m.GetFactsAsync(sessionId, null, 0, 50))
                .ReturnsAsync(new List<McpMemoryServer.Models.Fact>
                {
                    new() { Id = 1, Content = "Test fact", Category = "test" }
                });

            // Act
            var response = await _mcpService.HandleRequestAsync(request, sessionId);

            // Assert
            Assert.NotNull(response);
            Assert.Null(response.Error);
        }

        #endregion

        #region Tool Call - Memory.Write Tests

        [Fact]
        public async Task HandleRequestAsync_MemoryWrite_Fact_CreatesFact()
        {
            // Arrange
            var sessionId = "test-session";
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 4,
                Method = "tools/call",
                Params = new CallToolParams
                {
                    Name = "memory.write",
                    Arguments = new Dictionary<string, object>
                    {
                        ["type"] = "fact",
                        ["content"] = "Test fact content",
                        ["category"] = "test",
                        ["session_id"] = sessionId
                    }
                }
            };

            _sessionManagerMock
                .Setup(m => m.IsSessionValidAsync(sessionId))
                .ReturnsAsync(true);

            _memoryServiceMock
                .Setup(m => m.CreateFactAsync(sessionId, "Test fact content", "test", null, 1.0))
                .ReturnsAsync(new McpMemoryServer.Models.Fact
                {
                    Id = 1,
                    Content = "Test fact content",
                    Category = "test"
                });

            _vectorStoreMock
                .Setup(m => m.AddTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            // Act
            var response = await _mcpService.HandleRequestAsync(request, sessionId);

            // Assert
            Assert.NotNull(response);
            Assert.Null(response.Error);
            _memoryServiceMock.Verify(m => m.CreateFactAsync(sessionId, "Test fact content", "test", null, 1.0), Times.Once);
        }

        [Fact]
        public async Task HandleRequestAsync_MemoryWrite_MissingContent_ReturnsError()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 5,
                Method = "tools/call",
                Params = new CallToolParams
                {
                    Name = "memory.write",
                    Arguments = new Dictionary<string, object>
                    {
                        ["type"] = "fact"
                        // Missing content
                    }
                }
            };

            // Act
            var response = await _mcpService.HandleRequestAsync(request, "session");

            // Assert
            var result = JsonSerializer.Deserialize<CallToolResult>(
                JsonSerializer.Serialize(response.Result));
            Assert.NotNull(result);
            Assert.True(result.IsError);
        }

        #endregion

        #region Tool Call - Memory.Search Tests

        [Fact]
        public async Task HandleRequestAsync_MemorySearch_ReturnsResults()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 6,
                Method = "tools/call",
                Params = new CallToolParams
                {
                    Name = "memory.search",
                    Arguments = new Dictionary<string, object>
                    {
                        ["query"] = "test query",
                        ["limit"] = 5
                    }
                }
            };

            _vectorStoreMock
                .Setup(m => m.SearchAsync("test query", 5, null))
                .ReturnsAsync(new List<VectorSearchResult>
                {
                    new() { Id = "1", Content = "Result 1", Type = "fact", Similarity = 0.95 }
                });

            // Act
            var response = await _mcpService.HandleRequestAsync(request, "session");

            // Assert
            Assert.NotNull(response);
            Assert.Null(response.Error);
        }

        #endregion

        #region Tool Call - FileSystem Tests

        [Fact]
        public async Task HandleRequestAsync_FileSystemReadFile_ReturnsContent()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 7,
                Method = "tools/call",
                Params = new CallToolParams
                {
                    Name = "filesystem.read_file",
                    Arguments = new Dictionary<string, object>
                    {
                        ["path"] = "test.txt"
                    }
                }
            };

            _fileSystemMock
                .Setup(m => m.ReadFileAsync("test.txt"))
                .ReturnsAsync("File content");

            // Act
            var response = await _mcpService.HandleRequestAsync(request, "session");

            // Assert
            Assert.NotNull(response);
            Assert.Null(response.Error);
            _fileSystemMock.Verify(m => m.ReadFileAsync("test.txt"), Times.Once);
        }

        [Fact]
        public async Task HandleRequestAsync_FileSystemListDirectory_ReturnsItems()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 8,
                Method = "tools/call",
                Params = new CallToolParams
                {
                    Name = "filesystem.list_directory",
                    Arguments = new Dictionary<string, object>
                    {
                        ["path"] = ".",
                        ["recursive"] = false
                    }
                }
            };

            _fileSystemMock
                .Setup(m => m.ListDirectoryAsync(".", false))
                .ReturnsAsync(new List<McpMemoryServer.Services.FileInfo>
                {
                    new() { Name = "file1.txt", IsDirectory = false, Size = 100 }
                });

            // Act
            var response = await _mcpService.HandleRequestAsync(request, "session");

            // Assert
            Assert.NotNull(response);
            Assert.Null(response.Error);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task HandleRequestAsync_UnknownMethod_ReturnsError()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 9,
                Method = "unknown/method",
                Params = new { }
            };

            // Act
            var response = await _mcpService.HandleRequestAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Error);
            Assert.Equal(JsonRpcErrorCodes.MethodNotFound, response.Error.Code);
        }

        [Fact]
        public async Task HandleRequestAsync_InvalidToolName_ReturnsError()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 10,
                Method = "tools/call",
                Params = new CallToolParams
                {
                    Name = "invalid.tool",
                    Arguments = new Dictionary<string, object>()
                }
            };

            // Act
            var response = await _mcpService.HandleRequestAsync(request, "session");

            // Assert
            var result = JsonSerializer.Deserialize<CallToolResult>(
                JsonSerializer.Serialize(response.Result));
            Assert.NotNull(result);
            Assert.True(result.IsError);
        }

        [Fact]
        public async Task HandleRequestAsync_InvalidSession_ReturnsError()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 11,
                Method = "tools/call",
                Params = new CallToolParams
                {
                    Name = "memory.read",
                    Arguments = new Dictionary<string, object>
                    {
                        ["session_id"] = "invalid-session"
                    }
                }
            };

            _sessionManagerMock
                .Setup(m => m.IsSessionValidAsync("invalid-session"))
                .ReturnsAsync(false);

            // Act
            var response = await _mcpService.HandleRequestAsync(request, "invalid-session");

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Error);
            Assert.Equal(JsonRpcErrorCodes.InvalidParams, response.Error.Code);
        }

        #endregion

        #region GetInitializeResult Tests

        [Fact]
        public void GetInitializeResult_ReturnsCorrectProtocolVersion()
        {
            // Act
            var result = _mcpService.GetInitializeResult();

            // Assert
            Assert.Equal("2024-11-05", result.ProtocolVersion);
            Assert.Equal("McpMemoryServer", result.ServerInfo.Name);
            Assert.Equal("1.0.0", result.ServerInfo.Version);
        }

        #endregion
    }
}
