using McpMemoryServer.Models;

namespace McpMemoryServer.Services
{
    public interface ISessionManager
    {
        Task<Session> CreateSessionAsync(string clientInfo, string capabilities);
        Task<Session?> GetSessionAsync(string sessionId);
        Task UpdateSessionAccessAsync(string sessionId);
        Task<bool> IsSessionValidAsync(string sessionId);
        Task CleanupExpiredSessionsAsync();
    }
}
