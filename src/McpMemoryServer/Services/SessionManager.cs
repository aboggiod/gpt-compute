using Microsoft.EntityFrameworkCore;
using McpMemoryServer.Data;
using McpMemoryServer.Models;
using System.Collections.Concurrent;

namespace McpMemoryServer.Services
{
    public class SessionManager : ISessionManager
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SessionManager> _logger;
        private readonly ConcurrentDictionary<string, DateTime> _sessionCache;
        private readonly TimeSpan _sessionTimeout;

        public SessionManager(IServiceScopeFactory scopeFactory, ILogger<SessionManager> logger, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _sessionCache = new ConcurrentDictionary<string, DateTime>();

            var timeoutMinutes = configuration.GetValue<int>("Session:TimeoutMinutes", 60);
            _sessionTimeout = TimeSpan.FromMinutes(timeoutMinutes);

            // Start background cleanup task
            _ = Task.Run(async () => await BackgroundCleanupAsync());
        }

        public async Task<Session> CreateSessionAsync(string clientInfo, string capabilities)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();

            var sessionId = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow;

            var session = new Session
            {
                SessionId = sessionId,
                ClientInfo = clientInfo,
                Capabilities = capabilities,
                CreatedAt = now,
                LastAccessedAt = now,
                ExpiresAt = now.Add(_sessionTimeout)
            };

            context.Sessions.Add(session);
            await context.SaveChangesAsync();

            _sessionCache[sessionId] = now;
            _logger.LogInformation("Created new session: {SessionId}", sessionId);

            return session;
        }

        public async Task<Session?> GetSessionAsync(string sessionId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();

            return await context.Sessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        }

        public async Task UpdateSessionAccessAsync(string sessionId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();

            var session = await context.Sessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session != null)
            {
                var now = DateTime.UtcNow;
                session.LastAccessedAt = now;
                session.ExpiresAt = now.Add(_sessionTimeout);
                await context.SaveChangesAsync();

                _sessionCache[sessionId] = now;
            }
        }

        public async Task<bool> IsSessionValidAsync(string sessionId)
        {
            // Check cache first
            if (_sessionCache.TryGetValue(sessionId, out var lastAccess))
            {
                if (DateTime.UtcNow - lastAccess < _sessionTimeout)
                {
                    return true;
                }
            }

            // Check database
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();

            var session = await context.Sessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.ExpiresAt > DateTime.UtcNow);

            if (session != null)
            {
                _sessionCache[sessionId] = session.LastAccessedAt;
                return true;
            }

            _sessionCache.TryRemove(sessionId, out _);
            return false;
        }

        public async Task CleanupExpiredSessionsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();

            var expiredSessions = await context.Sessions
                .Where(s => s.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            if (expiredSessions.Any())
            {
                context.Sessions.RemoveRange(expiredSessions);
                await context.SaveChangesAsync();

                foreach (var session in expiredSessions)
                {
                    _sessionCache.TryRemove(session.SessionId, out _);
                }

                _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
            }
        }

        private async Task BackgroundCleanupAsync()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    await CleanupExpiredSessionsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during background session cleanup");
                }
            }
        }
    }
}
