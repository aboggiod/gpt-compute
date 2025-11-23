using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using McpMemoryServer.Mcp.Models;
using McpMemoryServer.Services;
using System.Text.Json;

namespace McpMemoryServer.Controllers
{
    [ApiController]
    [Route("mcp")]
    public class McpController : ControllerBase
    {
        private readonly IMcpService _mcpService;
        private readonly ILogger<McpController> _logger;

        public McpController(IMcpService mcpService, ILogger<McpController> logger)
        {
            _mcpService = mcpService;
            _logger = logger;
        }

        /// <summary>
        /// MCP JSON-RPC 2.0 endpoint
        /// Handles all MCP protocol requests: initialize, tools/list, tools/call
        /// </summary>
        [HttpPost]
        [Authorize]
        [Consumes("application/json")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonRpcResponse), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> HandleRequest([FromBody] JsonElement requestBody)
        {
            try
            {
                // Parse JSON-RPC request
                JsonRpcRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<JsonRpcRequest>(requestBody.GetRawText());
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse JSON-RPC request");
                    return BadRequest(new JsonRpcResponse
                    {
                        Id = null,
                        Error = new JsonRpcError
                        {
                            Code = JsonRpcErrorCodes.ParseError,
                            Message = "Parse error: Invalid JSON"
                        }
                    });
                }

                if (request == null)
                {
                    return BadRequest(new JsonRpcResponse
                    {
                        Id = null,
                        Error = new JsonRpcError
                        {
                            Code = JsonRpcErrorCodes.InvalidRequest,
                            Message = "Invalid request"
                        }
                    });
                }

                // Validate JSON-RPC 2.0 format
                if (request.JsonRpc != "2.0")
                {
                    return BadRequest(new JsonRpcResponse
                    {
                        Id = request.Id,
                        Error = new JsonRpcError
                        {
                            Code = JsonRpcErrorCodes.InvalidRequest,
                            Message = "Invalid JSON-RPC version (must be 2.0)"
                        }
                    });
                }

                // Get session ID from custom header or claims
                var sessionId = Request.Headers["X-Session-Id"].FirstOrDefault() ??
                               User.Claims.FirstOrDefault(c => c.Type == "session_id")?.Value;

                // Handle the request
                var response = await _mcpService.HandleRequestAsync(request, sessionId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in MCP endpoint");
                return StatusCode(500, new JsonRpcResponse
                {
                    Id = null,
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.InternalError,
                        Message = "Internal server error"
                    }
                });
            }
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        [ProducesResponseType(200)]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                service = "McpMemoryServer",
                version = "1.0.0",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// MCP server info endpoint (for discovery)
        /// </summary>
        [HttpGet("info")]
        [AllowAnonymous]
        [ProducesResponseType(200)]
        public IActionResult Info()
        {
            var initResult = _mcpService.GetInitializeResult();
            var tools = _mcpService.GetAvailableTools();

            return Ok(new
            {
                protocolVersion = initResult.ProtocolVersion,
                serverInfo = initResult.ServerInfo,
                capabilities = initResult.Capabilities,
                tools = tools.Select(t => new
                {
                    t.Name,
                    t.Description
                }),
                authentication = new
                {
                    type = "oauth2",
                    tokenEndpoint = "/oauth/token",
                    grantType = "client_credentials",
                    scopes = new[] { "memory.read", "memory.write", "tools.filesystem" }
                }
            });
        }
    }
}
