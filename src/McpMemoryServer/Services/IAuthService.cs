using McpMemoryServer.Models;

namespace McpMemoryServer.Services
{
    public interface IAuthService
    {
        Task<string?> AuthenticateAsync(string clientId, string clientSecret, string[] scopes);
        Task<OAuthClient?> ValidateClientAsync(string clientId);
        Task<bool> ValidateClientSecretAsync(string clientId, string clientSecret);
    }
}
