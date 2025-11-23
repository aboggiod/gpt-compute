using McpMemoryServer.Mcp.Models;

namespace McpMemoryServer.Services
{
    public interface IMcpService
    {
        Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request, string? sessionId = null);
        InitializeResult GetInitializeResult();
        List<Tool> GetAvailableTools();
    }
}
