using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using McpMemoryServer.Data;
using McpMemoryServer.Services;
using Xunit;

namespace McpMemoryServer.Tests.Services
{
    public class SessionManagerTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly SessionManager _sessionManager;
        private readonly Mock<ILogger<SessionManager>> _loggerMock;

        public SessionManagerTests()
        {
            var services = new ServiceCollection();

            // Setup in-memory database
            services.AddDbContext<MemoryDbContext>(options =>
                options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));

            var configDict = new Dictionary<string, string>
            {
                ["Session:TimeoutMinutes"] = "5"
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configDict!)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);

            _serviceProvider = services.BuildServiceProvider();

            _loggerMock = new Mock<ILogger<SessionManager>>();
            _sessionManager = new SessionManager(
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                _loggerMock.Object,
                configuration);
        }

        [Fact]
        public async Task CreateSessionAsync_ValidData_CreatesSession()
        {
            // Arrange
            var clientInfo = "Test Client";
            var capabilities = "tools";

            // Act
            var session = await _sessionManager.CreateSessionAsync(clientInfo, capabilities);

            // Assert
            Assert.NotNull(session);
            Assert.NotEmpty(session.SessionId);
            Assert.Equal(clientInfo, session.ClientInfo);
            Assert.Equal(capabilities, session.Capabilities);
        }

        [Fact]
        public async Task GetSessionAsync_ExistingSession_ReturnsSession()
        {
            // Arrange
            var created = await _sessionManager.CreateSessionAsync("Client", "Caps");

            // Act
            var retrieved = await _sessionManager.GetSessionAsync(created.SessionId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(created.SessionId, retrieved.SessionId);
        }

        [Fact]
        public async Task GetSessionAsync_NonExistentSession_ReturnsNull()
        {
            // Act
            var retrieved = await _sessionManager.GetSessionAsync("nonexistent");

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task IsSessionValidAsync_ValidSession_ReturnsTrue()
        {
            // Arrange
            var session = await _sessionManager.CreateSessionAsync("Client", "Caps");

            // Act
            var isValid = await _sessionManager.IsSessionValidAsync(session.SessionId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task IsSessionValidAsync_NonExistentSession_ReturnsFalse()
        {
            // Act
            var isValid = await _sessionManager.IsSessionValidAsync("nonexistent");

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task UpdateSessionAccessAsync_UpdatesTimestamps()
        {
            // Arrange
            var session = await _sessionManager.CreateSessionAsync("Client", "Caps");
            var originalLastAccessed = session.LastAccessedAt;
            var originalExpires = session.ExpiresAt;
            await Task.Delay(100);

            // Act
            await _sessionManager.UpdateSessionAccessAsync(session.SessionId);

            // Assert
            var updated = await _sessionManager.GetSessionAsync(session.SessionId);
            Assert.NotNull(updated);
            Assert.True(updated.LastAccessedAt > originalLastAccessed);
            Assert.True(updated.ExpiresAt > originalExpires);
        }

        [Fact]
        public async Task CleanupExpiredSessionsAsync_RemovesExpiredSessions()
        {
            // Arrange
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();

                // Create an expired session directly in the database
                var expiredSession = new McpMemoryServer.Models.Session
                {
                    SessionId = "expired-session",
                    CreatedAt = DateTime.UtcNow.AddHours(-2),
                    LastAccessedAt = DateTime.UtcNow.AddHours(-2),
                    ExpiresAt = DateTime.UtcNow.AddHours(-1) // Expired 1 hour ago
                };
                context.Sessions.Add(expiredSession);
                await context.SaveChangesAsync();
            }

            // Act
            await _sessionManager.CleanupExpiredSessionsAsync();

            // Assert
            var retrieved = await _sessionManager.GetSessionAsync("expired-session");
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task CreateSessionAsync_GeneratesUniqueSessionIds()
        {
            // Arrange & Act
            var session1 = await _sessionManager.CreateSessionAsync("Client1", "Caps1");
            var session2 = await _sessionManager.CreateSessionAsync("Client2", "Caps2");

            // Assert
            Assert.NotEqual(session1.SessionId, session2.SessionId);
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
        }
    }
}
