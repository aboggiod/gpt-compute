using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using McpMemoryServer.Controllers;
using McpMemoryServer.Mcp.Models;
using McpMemoryServer.Services;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace McpMemoryServer.Tests.Controllers
{
    public class McpControllerTests
    {
        private readonly Mock<IMcpService> _mcpServiceMock;
        private readonly Mock<ILogger<McpController>> _loggerMock;
        private readonly McpController _controller;

        public McpControllerTests()
        {
            _mcpServiceMock = new Mock<IMcpService>();
            _loggerMock = new Mock<ILogger<McpController>>();
            _controller = new McpController(_mcpServiceMock.Object, _loggerMock.Object);

            // Setup controller context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Fact]
        public async Task HandleRequest_ValidRequest_ReturnsOk()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 1,
                Method = "tools/list",
                Params = new { }
            };

            var expectedResponse = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 1,
                Result = new ListToolsResult { Tools = new List<Tool>() }
            };

            _mcpServiceMock
                .Setup(m => m.HandleRequestAsync(It.IsAny<JsonRpcRequest>(), null))
                .ReturnsAsync(expectedResponse);

            var requestBody = JsonSerializer.SerializeToElement(request);

            // Act
            var result = await _controller.HandleRequest(requestBody);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<JsonRpcResponse>(okResult.Value);
            Assert.Equal(expectedResponse.Id, response.Id);
        }

        [Fact]
        public async Task HandleRequest_InvalidJson_ReturnsBadRequest()
        {
            // Arrange
            var invalidJson = JsonSerializer.SerializeToElement("{invalid json}");

            // Act
            var result = await _controller.HandleRequest(invalidJson);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<JsonRpcResponse>(badRequestResult.Value);
            Assert.NotNull(response.Error);
            Assert.Equal(JsonRpcErrorCodes.ParseError, response.Error.Code);
        }

        [Fact]
        public async Task HandleRequest_InvalidJsonRpcVersion_ReturnsBadRequest()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                JsonRpc = "1.0", // Invalid version
                Id = 1,
                Method = "test",
                Params = new { }
            };

            var requestBody = JsonSerializer.SerializeToElement(request);

            // Act
            var result = await _controller.HandleRequest(requestBody);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<JsonRpcResponse>(badRequestResult.Value);
            Assert.NotNull(response.Error);
            Assert.Equal(JsonRpcErrorCodes.InvalidRequest, response.Error.Code);
        }

        [Fact]
        public async Task HandleRequest_WithSessionHeader_PassesSessionId()
        {
            // Arrange
            var sessionId = "test-session-id";
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 1,
                Method = "tools/list",
                Params = new { }
            };

            _controller.ControllerContext.HttpContext.Request.Headers["X-Session-Id"] = sessionId;

            _mcpServiceMock
                .Setup(m => m.HandleRequestAsync(It.IsAny<JsonRpcRequest>(), sessionId))
                .ReturnsAsync(new JsonRpcResponse { JsonRpc = "2.0", Id = 1 });

            var requestBody = JsonSerializer.SerializeToElement(request);

            // Act
            await _controller.HandleRequest(requestBody);

            // Assert
            _mcpServiceMock.Verify(
                m => m.HandleRequestAsync(It.IsAny<JsonRpcRequest>(), sessionId),
                Times.Once);
        }

        [Fact]
        public async Task HandleRequest_WithSessionClaim_PassesSessionId()
        {
            // Arrange
            var sessionId = "claim-session-id";
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 1,
                Method = "tools/list",
                Params = new { }
            };

            var claims = new List<Claim>
            {
                new Claim("session_id", sessionId)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext.HttpContext.User = claimsPrincipal;

            _mcpServiceMock
                .Setup(m => m.HandleRequestAsync(It.IsAny<JsonRpcRequest>(), sessionId))
                .ReturnsAsync(new JsonRpcResponse { JsonRpc = "2.0", Id = 1 });

            var requestBody = JsonSerializer.SerializeToElement(request);

            // Act
            await _controller.HandleRequest(requestBody);

            // Assert
            _mcpServiceMock.Verify(
                m => m.HandleRequestAsync(It.IsAny<JsonRpcRequest>(), sessionId),
                Times.Once);
        }

        [Fact]
        public void Health_ReturnsOk()
        {
            // Act
            var result = _controller.Health();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public void Info_ReturnsServerInfo()
        {
            // Arrange
            _mcpServiceMock
                .Setup(m => m.GetInitializeResult())
                .Returns(new InitializeResult
                {
                    ProtocolVersion = "2024-11-05",
                    ServerInfo = new Implementation { Name = "TestServer", Version = "1.0" },
                    Capabilities = new ServerCapabilities()
                });

            _mcpServiceMock
                .Setup(m => m.GetAvailableTools())
                .Returns(new List<Tool>
                {
                    new Tool { Name = "test.tool", Description = "Test tool" }
                });

            // Act
            var result = _controller.Info();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }
    }
}
