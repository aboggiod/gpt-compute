using Microsoft.EntityFrameworkCore;
using McpMemoryServer.Data;
using McpMemoryServer.Models;

namespace McpMemoryServer.Services
{
    public class MemoryService : IMemoryService
    {
        private readonly MemoryDbContext _context;
        private readonly ILogger<MemoryService> _logger;

        public MemoryService(MemoryDbContext context, ILogger<MemoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Conversations
        public async Task<Conversation> CreateConversationAsync(string sessionId, string title)
        {
            var conversation = new Conversation
            {
                SessionId = sessionId,
                Title = title,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created conversation {ConversationId} for session {SessionId}", conversation.Id, sessionId);
            return conversation;
        }

        public async Task<Conversation?> GetConversationAsync(int id)
        {
            return await _context.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<IEnumerable<Conversation>> GetConversationsAsync(string sessionId, int skip = 0, int take = 50)
        {
            return await _context.Conversations
                .Where(c => c.SessionId == sessionId)
                .OrderByDescending(c => c.UpdatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        // Messages
        public async Task<Message> AddMessageAsync(int conversationId, string role, string content, string? metadata = null)
        {
            var message = new Message
            {
                ConversationId = conversationId,
                Role = role,
                Content = content,
                Timestamp = DateTime.UtcNow,
                Metadata = metadata
            };

            _context.Messages.Add(message);

            // Update conversation's UpdatedAt
            var conversation = await _context.Conversations.FindAsync(conversationId);
            if (conversation != null)
            {
                conversation.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Added {Role} message to conversation {ConversationId}", role, conversationId);
            return message;
        }

        public async Task<IEnumerable<Message>> GetMessagesAsync(int conversationId, int skip = 0, int take = 100)
        {
            return await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.Timestamp)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        // User Preferences
        public async Task<UserPreference> SetPreferenceAsync(string sessionId, string key, string value, string? category = null)
        {
            var existing = await _context.UserPreferences
                .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.Key == key);

            if (existing != null)
            {
                existing.Value = value;
                existing.Category = category ?? existing.Category;
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated preference {Key} for session {SessionId}", key, sessionId);
                return existing;
            }

            var preference = new UserPreference
            {
                SessionId = sessionId,
                Key = key,
                Value = value,
                Category = category,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserPreferences.Add(preference);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created preference {Key} for session {SessionId}", key, sessionId);
            return preference;
        }

        public async Task<UserPreference?> GetPreferenceAsync(string sessionId, string key)
        {
            return await _context.UserPreferences
                .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.Key == key);
        }

        public async Task<IEnumerable<UserPreference>> GetPreferencesAsync(string sessionId, string? category = null)
        {
            var query = _context.UserPreferences.Where(p => p.SessionId == sessionId);

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
            }

            return await query.OrderBy(p => p.Key).ToListAsync();
        }

        // Facts
        public async Task<Fact> CreateFactAsync(string sessionId, string content, string? category = null, string? source = null, double confidence = 1.0)
        {
            var fact = new Fact
            {
                SessionId = sessionId,
                Content = content,
                Category = category,
                Source = source,
                Confidence = confidence,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Facts.Add(fact);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created fact {FactId} for session {SessionId}", fact.Id, sessionId);
            return fact;
        }

        public async Task<Fact?> GetFactAsync(int id)
        {
            return await _context.Facts.FindAsync(id);
        }

        public async Task<IEnumerable<Fact>> GetFactsAsync(string sessionId, string? category = null, int skip = 0, int take = 100)
        {
            var query = _context.Facts.Where(f => f.SessionId == sessionId);

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(f => f.Category == category);
            }

            return await query
                .OrderByDescending(f => f.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<Fact> UpdateFactAsync(int id, string content, double? confidence = null)
        {
            var fact = await _context.Facts.FindAsync(id);
            if (fact == null)
            {
                throw new KeyNotFoundException($"Fact with ID {id} not found");
            }

            fact.Content = content;
            if (confidence.HasValue)
            {
                fact.Confidence = confidence.Value;
            }
            fact.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated fact {FactId}", id);
            return fact;
        }

        public async Task<bool> DeleteFactAsync(int id)
        {
            var fact = await _context.Facts.FindAsync(id);
            if (fact == null)
            {
                return false;
            }

            _context.Facts.Remove(fact);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted fact {FactId}", id);
            return true;
        }
    }
}
