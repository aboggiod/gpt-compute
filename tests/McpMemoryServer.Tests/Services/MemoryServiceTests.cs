using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using McpMemoryServer.Data;
using McpMemoryServer.Services;
using Xunit;

namespace McpMemoryServer.Tests.Services
{
    public class MemoryServiceTests : IDisposable
    {
        private readonly MemoryDbContext _context;
        private readonly MemoryService _memoryService;
        private readonly Mock<ILogger<MemoryService>> _loggerMock;

        public MemoryServiceTests()
        {
            var options = new DbContextOptionsBuilder<MemoryDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new MemoryDbContext(options);
            _loggerMock = new Mock<ILogger<MemoryService>>();
            _memoryService = new MemoryService(_context, _loggerMock.Object);
        }

        #region Conversation Tests

        [Fact]
        public async Task CreateConversationAsync_ValidData_CreatesConversation()
        {
            // Arrange
            var sessionId = "test-session";
            var title = "Test Conversation";

            // Act
            var conversation = await _memoryService.CreateConversationAsync(sessionId, title);

            // Assert
            Assert.NotNull(conversation);
            Assert.Equal(sessionId, conversation.SessionId);
            Assert.Equal(title, conversation.Title);
            Assert.True(conversation.Id > 0);
        }

        [Fact]
        public async Task GetConversationAsync_ExistingId_ReturnsConversation()
        {
            // Arrange
            var created = await _memoryService.CreateConversationAsync("session1", "Test");

            // Act
            var retrieved = await _memoryService.GetConversationAsync(created.Id);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(created.Id, retrieved.Id);
        }

        [Fact]
        public async Task GetConversationAsync_NonExistentId_ReturnsNull()
        {
            // Act
            var retrieved = await _memoryService.GetConversationAsync(9999);

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task GetConversationsAsync_ReturnsSessionConversations()
        {
            // Arrange
            await _memoryService.CreateConversationAsync("session1", "Conv1");
            await _memoryService.CreateConversationAsync("session1", "Conv2");
            await _memoryService.CreateConversationAsync("session2", "Conv3");

            // Act
            var conversations = await _memoryService.GetConversationsAsync("session1");

            // Assert
            Assert.Equal(2, conversations.Count());
        }

        #endregion

        #region Message Tests

        [Fact]
        public async Task AddMessageAsync_ValidData_AddsMessage()
        {
            // Arrange
            var conversation = await _memoryService.CreateConversationAsync("session1", "Test");

            // Act
            var message = await _memoryService.AddMessageAsync(conversation.Id, "user", "Hello");

            // Assert
            Assert.NotNull(message);
            Assert.Equal("user", message.Role);
            Assert.Equal("Hello", message.Content);
        }

        [Fact]
        public async Task AddMessageAsync_UpdatesConversationTimestamp()
        {
            // Arrange
            var conversation = await _memoryService.CreateConversationAsync("session1", "Test");
            var originalUpdatedAt = conversation.UpdatedAt;
            await Task.Delay(10);

            // Act
            await _memoryService.AddMessageAsync(conversation.Id, "user", "Hello");

            // Assert
            var updated = await _memoryService.GetConversationAsync(conversation.Id);
            Assert.NotNull(updated);
            Assert.True(updated.UpdatedAt > originalUpdatedAt);
        }

        [Fact]
        public async Task GetMessagesAsync_ReturnsConversationMessages()
        {
            // Arrange
            var conversation = await _memoryService.CreateConversationAsync("session1", "Test");
            await _memoryService.AddMessageAsync(conversation.Id, "user", "Message 1");
            await _memoryService.AddMessageAsync(conversation.Id, "assistant", "Message 2");

            // Act
            var messages = await _memoryService.GetMessagesAsync(conversation.Id);

            // Assert
            Assert.Equal(2, messages.Count());
        }

        #endregion

        #region User Preference Tests

        [Fact]
        public async Task SetPreferenceAsync_NewPreference_CreatesPreference()
        {
            // Arrange
            var sessionId = "session1";
            var key = "theme";
            var value = "dark";

            // Act
            var preference = await _memoryService.SetPreferenceAsync(sessionId, key, value);

            // Assert
            Assert.NotNull(preference);
            Assert.Equal(key, preference.Key);
            Assert.Equal(value, preference.Value);
        }

        [Fact]
        public async Task SetPreferenceAsync_ExistingPreference_UpdatesPreference()
        {
            // Arrange
            var sessionId = "session1";
            var key = "theme";
            await _memoryService.SetPreferenceAsync(sessionId, key, "dark");

            // Act
            var updated = await _memoryService.SetPreferenceAsync(sessionId, key, "light");

            // Assert
            Assert.Equal("light", updated.Value);
            var allPrefs = await _memoryService.GetPreferencesAsync(sessionId);
            Assert.Single(allPrefs);
        }

        [Fact]
        public async Task GetPreferenceAsync_ExistingKey_ReturnsPreference()
        {
            // Arrange
            var sessionId = "session1";
            var key = "language";
            await _memoryService.SetPreferenceAsync(sessionId, key, "en");

            // Act
            var preference = await _memoryService.GetPreferenceAsync(sessionId, key);

            // Assert
            Assert.NotNull(preference);
            Assert.Equal("en", preference.Value);
        }

        [Fact]
        public async Task GetPreferenceAsync_NonExistentKey_ReturnsNull()
        {
            // Act
            var preference = await _memoryService.GetPreferenceAsync("session1", "nonexistent");

            // Assert
            Assert.Null(preference);
        }

        [Fact]
        public async Task GetPreferencesAsync_FiltersbyCategory()
        {
            // Arrange
            var sessionId = "session1";
            await _memoryService.SetPreferenceAsync(sessionId, "theme", "dark", "ui");
            await _memoryService.SetPreferenceAsync(sessionId, "lang", "en", "locale");
            await _memoryService.SetPreferenceAsync(sessionId, "font", "arial", "ui");

            // Act
            var uiPrefs = await _memoryService.GetPreferencesAsync(sessionId, "ui");

            // Assert
            Assert.Equal(2, uiPrefs.Count());
        }

        #endregion

        #region Fact Tests

        [Fact]
        public async Task CreateFactAsync_ValidData_CreatesFact()
        {
            // Arrange
            var sessionId = "session1";
            var content = "User prefers Python programming";

            // Act
            var fact = await _memoryService.CreateFactAsync(sessionId, content);

            // Assert
            Assert.NotNull(fact);
            Assert.Equal(content, fact.Content);
            Assert.Equal(1.0, fact.Confidence);
        }

        [Fact]
        public async Task GetFactAsync_ExistingId_ReturnsFact()
        {
            // Arrange
            var created = await _memoryService.CreateFactAsync("session1", "Test fact");

            // Act
            var retrieved = await _memoryService.GetFactAsync(created.Id);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(created.Id, retrieved.Id);
        }

        [Fact]
        public async Task GetFactsAsync_ReturnsSessionFacts()
        {
            // Arrange
            await _memoryService.CreateFactAsync("session1", "Fact 1");
            await _memoryService.CreateFactAsync("session1", "Fact 2");
            await _memoryService.CreateFactAsync("session2", "Fact 3");

            // Act
            var facts = await _memoryService.GetFactsAsync("session1");

            // Assert
            Assert.Equal(2, facts.Count());
        }

        [Fact]
        public async Task GetFactsAsync_FiltersByCategory()
        {
            // Arrange
            await _memoryService.CreateFactAsync("session1", "Fact 1", "personal");
            await _memoryService.CreateFactAsync("session1", "Fact 2", "work");
            await _memoryService.CreateFactAsync("session1", "Fact 3", "personal");

            // Act
            var personalFacts = await _memoryService.GetFactsAsync("session1", "personal");

            // Assert
            Assert.Equal(2, personalFacts.Count());
        }

        [Fact]
        public async Task UpdateFactAsync_ExistingFact_UpdatesContent()
        {
            // Arrange
            var fact = await _memoryService.CreateFactAsync("session1", "Original content");

            // Act
            var updated = await _memoryService.UpdateFactAsync(fact.Id, "Updated content", 0.8);

            // Assert
            Assert.Equal("Updated content", updated.Content);
            Assert.Equal(0.8, updated.Confidence);
        }

        [Fact]
        public async Task UpdateFactAsync_NonExistentFact_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
                await _memoryService.UpdateFactAsync(9999, "Content"));
        }

        [Fact]
        public async Task DeleteFactAsync_ExistingFact_ReturnsTrue()
        {
            // Arrange
            var fact = await _memoryService.CreateFactAsync("session1", "Test fact");

            // Act
            var result = await _memoryService.DeleteFactAsync(fact.Id);

            // Assert
            Assert.True(result);
            var deleted = await _memoryService.GetFactAsync(fact.Id);
            Assert.Null(deleted);
        }

        [Fact]
        public async Task DeleteFactAsync_NonExistentFact_ReturnsFalse()
        {
            // Act
            var result = await _memoryService.DeleteFactAsync(9999);

            // Assert
            Assert.False(result);
        }

        #endregion

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
