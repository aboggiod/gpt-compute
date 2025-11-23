using McpMemoryServer.Models;

namespace McpMemoryServer.Services
{
    public interface IMemoryService
    {
        // Conversations
        Task<Conversation> CreateConversationAsync(string sessionId, string title);
        Task<Conversation?> GetConversationAsync(int id);
        Task<IEnumerable<Conversation>> GetConversationsAsync(string sessionId, int skip = 0, int take = 50);

        // Messages
        Task<Message> AddMessageAsync(int conversationId, string role, string content, string? metadata = null);
        Task<IEnumerable<Message>> GetMessagesAsync(int conversationId, int skip = 0, int take = 100);

        // User Preferences
        Task<UserPreference> SetPreferenceAsync(string sessionId, string key, string value, string? category = null);
        Task<UserPreference?> GetPreferenceAsync(string sessionId, string key);
        Task<IEnumerable<UserPreference>> GetPreferencesAsync(string sessionId, string? category = null);

        // Facts
        Task<Fact> CreateFactAsync(string sessionId, string content, string? category = null, string? source = null, double confidence = 1.0);
        Task<Fact?> GetFactAsync(int id);
        Task<IEnumerable<Fact>> GetFactsAsync(string sessionId, string? category = null, int skip = 0, int take = 100);
        Task<Fact> UpdateFactAsync(int id, string content, double? confidence = null);
        Task<bool> DeleteFactAsync(int id);
    }
}
