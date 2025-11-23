using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using McpMemoryServer.Data;
using McpMemoryServer.Models;

namespace McpMemoryServer.Services
{
    public class AuthService : IAuthService
    {
        private readonly MemoryDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(MemoryDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string?> AuthenticateAsync(string clientId, string clientSecret, string[] scopes)
        {
            try
            {
                var client = await ValidateClientAsync(clientId);
                if (client == null || !client.IsActive)
                {
                    _logger.LogWarning("Authentication failed: Invalid or inactive client {ClientId}", clientId);
                    return null;
                }

                if (!await ValidateClientSecretAsync(clientId, clientSecret))
                {
                    _logger.LogWarning("Authentication failed: Invalid secret for client {ClientId}", clientId);
                    return null;
                }

                // Validate requested scopes
                var clientScopes = client.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var invalidScopes = scopes.Except(clientScopes).ToArray();
                if (invalidScopes.Any())
                {
                    _logger.LogWarning("Authentication failed: Invalid scopes requested {InvalidScopes}", string.Join(", ", invalidScopes));
                    return null;
                }

                // Update last used timestamp
                client.LastUsedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Generate JWT token
                var token = GenerateJwtToken(client, scopes);
                _logger.LogInformation("Successfully authenticated client {ClientId} with scopes {Scopes}", clientId, string.Join(", ", scopes));

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication for client {ClientId}", clientId);
                return null;
            }
        }

        public async Task<OAuthClient?> ValidateClientAsync(string clientId)
        {
            return await _context.OAuthClients
                .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);
        }

        public async Task<bool> ValidateClientSecretAsync(string clientId, string clientSecret)
        {
            var client = await _context.OAuthClients
                .FirstOrDefaultAsync(c => c.ClientId == clientId);

            if (client == null)
                return false;

            // For production, use BCrypt or similar
            // For now, using simple comparison (CHANGE IN PRODUCTION!)
            return client.ClientSecret == HashSecret(clientSecret) ||
                   client.ClientSecret == clientSecret; // Allow unhashed for dev
        }

        private string GenerateJwtToken(OAuthClient client, string[] scopes)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "your-super-secret-key-change-this-in-production-min-32-chars";
            var key = Encoding.ASCII.GetBytes(jwtKey);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, client.ClientId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("client_id", client.ClientId),
                new Claim("client_name", client.Name)
            };

            // Add each scope as a separate claim
            foreach (var scope in scopes)
            {
                claims.Add(new Claim("scope", scope));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(24),
                Issuer = _configuration["Jwt:Issuer"] ?? "McpMemoryServer",
                Audience = _configuration["Jwt:Audience"] ?? "McpMemoryClient",
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string HashSecret(string secret)
        {
            // Simple SHA256 hash (use BCrypt in production!)
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(secret));
            return Convert.ToBase64String(bytes);
        }
    }
}
