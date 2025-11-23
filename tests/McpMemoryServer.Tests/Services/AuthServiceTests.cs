using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using McpMemoryServer.Data;
using McpMemoryServer.Models;
using McpMemoryServer.Services;
using Xunit;

namespace McpMemoryServer.Tests.Services
{
    public class AuthServiceTests : IDisposable
    {
        private readonly MemoryDbContext _context;
        private readonly AuthService _authService;
        private readonly Mock<ILogger<AuthService>> _loggerMock;
        private readonly IConfiguration _configuration;

        public AuthServiceTests()
        {
            var options = new DbContextOptionsBuilder<MemoryDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new MemoryDbContext(options);

            var configDict = new Dictionary<string, string>
            {
                ["Jwt:Key"] = "test-key-with-minimum-32-characters-for-security",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience"
            };
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configDict!)
                .Build();

            _loggerMock = new Mock<ILogger<AuthService>>();
            _authService = new AuthService(_context, _configuration, _loggerMock.Object);

            SeedTestData();
        }

        private void SeedTestData()
        {
            _context.OAuthClients.Add(new OAuthClient
            {
                Id = 1,
                ClientId = "test-client",
                ClientSecret = "test-secret",
                Name = "Test Client",
                Scopes = "memory.read memory.write tools.filesystem",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            _context.OAuthClients.Add(new OAuthClient
            {
                Id = 2,
                ClientId = "inactive-client",
                ClientSecret = "inactive-secret",
                Name = "Inactive Client",
                Scopes = "memory.read",
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();
        }

        [Fact]
        public async Task AuthenticateAsync_ValidCredentials_ReturnsToken()
        {
            // Arrange
            var clientId = "test-client";
            var clientSecret = "test-secret";
            var scopes = new[] { "memory.read", "memory.write" };

            // Act
            var token = await _authService.AuthenticateAsync(clientId, clientSecret, scopes);

            // Assert
            Assert.NotNull(token);
            Assert.NotEmpty(token);
        }

        [Fact]
        public async Task AuthenticateAsync_InvalidClientId_ReturnsNull()
        {
            // Arrange
            var clientId = "invalid-client";
            var clientSecret = "test-secret";
            var scopes = new[] { "memory.read" };

            // Act
            var token = await _authService.AuthenticateAsync(clientId, clientSecret, scopes);

            // Assert
            Assert.Null(token);
        }

        [Fact]
        public async Task AuthenticateAsync_InvalidClientSecret_ReturnsNull()
        {
            // Arrange
            var clientId = "test-client";
            var clientSecret = "wrong-secret";
            var scopes = new[] { "memory.read" };

            // Act
            var token = await _authService.AuthenticateAsync(clientId, clientSecret, scopes);

            // Assert
            Assert.Null(token);
        }

        [Fact]
        public async Task AuthenticateAsync_InactiveClient_ReturnsNull()
        {
            // Arrange
            var clientId = "inactive-client";
            var clientSecret = "inactive-secret";
            var scopes = new[] { "memory.read" };

            // Act
            var token = await _authService.AuthenticateAsync(clientId, clientSecret, scopes);

            // Assert
            Assert.Null(token);
        }

        [Fact]
        public async Task AuthenticateAsync_InvalidScope_ReturnsNull()
        {
            // Arrange
            var clientId = "test-client";
            var clientSecret = "test-secret";
            var scopes = new[] { "invalid.scope" };

            // Act
            var token = await _authService.AuthenticateAsync(clientId, clientSecret, scopes);

            // Assert
            Assert.Null(token);
        }

        [Fact]
        public async Task AuthenticateAsync_UpdatesLastUsedAt()
        {
            // Arrange
            var clientId = "test-client";
            var clientSecret = "test-secret";
            var scopes = new[] { "memory.read" };
            var client = await _context.OAuthClients.FirstAsync(c => c.ClientId == clientId);
            var originalLastUsed = client.LastUsedAt;

            // Act
            await Task.Delay(10); // Small delay to ensure timestamp changes
            await _authService.AuthenticateAsync(clientId, clientSecret, scopes);

            // Assert
            await _context.Entry(client).ReloadAsync();
            Assert.NotEqual(originalLastUsed, client.LastUsedAt);
        }

        [Fact]
        public async Task ValidateClientAsync_ValidClient_ReturnsClient()
        {
            // Arrange
            var clientId = "test-client";

            // Act
            var client = await _authService.ValidateClientAsync(clientId);

            // Assert
            Assert.NotNull(client);
            Assert.Equal(clientId, client.ClientId);
        }

        [Fact]
        public async Task ValidateClientAsync_InvalidClient_ReturnsNull()
        {
            // Arrange
            var clientId = "nonexistent-client";

            // Act
            var client = await _authService.ValidateClientAsync(clientId);

            // Assert
            Assert.Null(client);
        }

        [Fact]
        public async Task ValidateClientSecretAsync_ValidSecret_ReturnsTrue()
        {
            // Arrange
            var clientId = "test-client";
            var clientSecret = "test-secret";

            // Act
            var isValid = await _authService.ValidateClientSecretAsync(clientId, clientSecret);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task ValidateClientSecretAsync_InvalidSecret_ReturnsFalse()
        {
            // Arrange
            var clientId = "test-client";
            var clientSecret = "wrong-secret";

            // Act
            var isValid = await _authService.ValidateClientSecretAsync(clientId, clientSecret);

            // Assert
            Assert.False(isValid);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
