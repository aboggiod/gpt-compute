using Microsoft.AspNetCore.Mvc;
using McpMemoryServer.Services;
using System.ComponentModel.DataAnnotations;

namespace McpMemoryServer.Controllers
{
    [ApiController]
    [Route("oauth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// OAuth 2.1 Token Endpoint (Client Credentials Flow)
        /// </summary>
        [HttpPost("token")]
        [ProducesResponseType(typeof(TokenResponse), 200)]
        [ProducesResponseType(typeof(OAuth2Error), 400)]
        [ProducesResponseType(typeof(OAuth2Error), 401)]
        public async Task<IActionResult> Token([FromForm] TokenRequest request)
        {
            _logger.LogInformation("Token request from client: {ClientId}", request.ClientId);

            // Validate grant type
            if (request.GrantType != "client_credentials")
            {
                return BadRequest(new OAuth2Error
                {
                    Error = "unsupported_grant_type",
                    ErrorDescription = "Only client_credentials grant type is supported"
                });
            }

            // Validate client credentials
            if (string.IsNullOrEmpty(request.ClientId) || string.IsNullOrEmpty(request.ClientSecret))
            {
                return BadRequest(new OAuth2Error
                {
                    Error = "invalid_request",
                    ErrorDescription = "client_id and client_secret are required"
                });
            }

            // Parse scopes
            var scopes = string.IsNullOrEmpty(request.Scope)
                ? new[] { "memory.read", "memory.write", "tools.filesystem" }
                : request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Authenticate and generate token
            var token = await _authService.AuthenticateAsync(request.ClientId, request.ClientSecret, scopes);

            if (token == null)
            {
                return Unauthorized(new OAuth2Error
                {
                    Error = "invalid_client",
                    ErrorDescription = "Invalid client credentials"
                });
            }

            return Ok(new TokenResponse
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = 86400, // 24 hours
                Scope = string.Join(' ', scopes)
            });
        }

        /// <summary>
        /// Token introspection endpoint (optional, for debugging)
        /// </summary>
        [HttpPost("introspect")]
        [ProducesResponseType(typeof(IntrospectResponse), 200)]
        public IActionResult Introspect([FromForm] IntrospectRequest request)
        {
            // Simplified introspection for demo
            // In production, validate the token properly
            return Ok(new IntrospectResponse
            {
                Active = !string.IsNullOrEmpty(request.Token),
                Scope = "memory.read memory.write tools.filesystem",
                ClientId = "chatgpt-client",
                TokenType = "Bearer"
            });
        }
    }

    public class TokenRequest
    {
        [Required]
        [FromForm(Name = "grant_type")]
        public string GrantType { get; set; } = "client_credentials";

        [Required]
        [FromForm(Name = "client_id")]
        public string ClientId { get; set; } = string.Empty;

        [Required]
        [FromForm(Name = "client_secret")]
        public string ClientSecret { get; set; } = string.Empty;

        [FromForm(Name = "scope")]
        public string? Scope { get; set; }
    }

    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; }
        public string Scope { get; set; } = string.Empty;
    }

    public class OAuth2Error
    {
        public string Error { get; set; } = string.Empty;
        public string ErrorDescription { get; set; } = string.Empty;
    }

    public class IntrospectRequest
    {
        [FromForm(Name = "token")]
        public string Token { get; set; } = string.Empty;
    }

    public class IntrospectResponse
    {
        public bool Active { get; set; }
        public string? Scope { get; set; }
        public string? ClientId { get; set; }
        public string? TokenType { get; set; }
        public long? Exp { get; set; }
        public long? Iat { get; set; }
    }
}
