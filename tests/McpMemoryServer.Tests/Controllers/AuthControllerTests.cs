using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using McpMemoryServer.Controllers;
using McpMemoryServer.Services;
using Xunit;

namespace McpMemoryServer.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IAuthService> _authServiceMock;
        private readonly Mock<ILogger<AuthController>> _loggerMock;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _authServiceMock = new Mock<IAuthService>();
            _loggerMock = new Mock<ILogger<AuthController>>();
            _controller = new AuthController(_authServiceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task Token_ValidCredentials_ReturnsOkWithToken()
        {
            // Arrange
            var request = new TokenRequest
            {
                GrantType = "client_credentials",
                ClientId = "test-client",
                ClientSecret = "test-secret",
                Scope = "memory.read memory.write"
            };

            _authServiceMock
                .Setup(m => m.AuthenticateAsync(request.ClientId, request.ClientSecret, It.IsAny<string[]>()))
                .ReturnsAsync("test-jwt-token");

            // Act
            var result = await _controller.Token(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<TokenResponse>(okResult.Value);
            Assert.Equal("test-jwt-token", response.AccessToken);
            Assert.Equal("Bearer", response.TokenType);
            Assert.Equal(86400, response.ExpiresIn);
        }

        [Fact]
        public async Task Token_InvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var request = new TokenRequest
            {
                GrantType = "client_credentials",
                ClientId = "invalid-client",
                ClientSecret = "invalid-secret"
            };

            _authServiceMock
                .Setup(m => m.AuthenticateAsync(request.ClientId, request.ClientSecret, It.IsAny<string[]>()))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _controller.Token(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var error = Assert.IsType<OAuth2Error>(unauthorizedResult.Value);
            Assert.Equal("invalid_client", error.Error);
        }

        [Fact]
        public async Task Token_UnsupportedGrantType_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenRequest
            {
                GrantType = "password",
                ClientId = "test-client",
                ClientSecret = "test-secret"
            };

            // Act
            var result = await _controller.Token(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var error = Assert.IsType<OAuth2Error>(badRequestResult.Value);
            Assert.Equal("unsupported_grant_type", error.Error);
        }

        [Fact]
        public async Task Token_MissingClientId_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenRequest
            {
                GrantType = "client_credentials",
                ClientId = "",
                ClientSecret = "test-secret"
            };

            // Act
            var result = await _controller.Token(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var error = Assert.IsType<OAuth2Error>(badRequestResult.Value);
            Assert.Equal("invalid_request", error.Error);
        }

        [Fact]
        public async Task Token_DefaultScopes_UsesAllScopes()
        {
            // Arrange
            var request = new TokenRequest
            {
                GrantType = "client_credentials",
                ClientId = "test-client",
                ClientSecret = "test-secret",
                Scope = null // No scopes provided
            };

            _authServiceMock
                .Setup(m => m.AuthenticateAsync(
                    request.ClientId,
                    request.ClientSecret,
                    It.Is<string[]>(s => s.Contains("memory.read") && s.Contains("memory.write") && s.Contains("tools.filesystem"))))
                .ReturnsAsync("test-token");

            // Act
            var result = await _controller.Token(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public void Introspect_ValidToken_ReturnsActive()
        {
            // Arrange
            var request = new IntrospectRequest
            {
                Token = "valid-token"
            };

            // Act
            var result = _controller.Introspect(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<IntrospectResponse>(okResult.Value);
            Assert.True(response.Active);
        }

        [Fact]
        public void Introspect_EmptyToken_ReturnsInactive()
        {
            // Arrange
            var request = new IntrospectRequest
            {
                Token = ""
            };

            // Act
            var result = _controller.Introspect(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<IntrospectResponse>(okResult.Value);
            Assert.False(response.Active);
        }
    }
}
