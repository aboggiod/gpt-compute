using System.Text.Json.Serialization;

namespace McpMemoryServer.Mcp.Models
{
    // Initialize request/response
    public class InitializeParams
    {
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; } = "2024-11-05";

        [JsonPropertyName("capabilities")]
        public ClientCapabilities Capabilities { get; set; } = new();

        [JsonPropertyName("clientInfo")]
        public Implementation ClientInfo { get; set; } = new();
    }

    public class InitializeResult
    {
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; } = "2024-11-05";

        [JsonPropertyName("capabilities")]
        public ServerCapabilities Capabilities { get; set; } = new();

        [JsonPropertyName("serverInfo")]
        public Implementation ServerInfo { get; set; } = new();
    }

    public class ClientCapabilities
    {
        [JsonPropertyName("tools")]
        public object? Tools { get; set; }

        [JsonPropertyName("resources")]
        public object? Resources { get; set; }

        [JsonPropertyName("prompts")]
        public object? Prompts { get; set; }
    }

    public class ServerCapabilities
    {
        [JsonPropertyName("tools")]
        public ToolsCapability? Tools { get; set; }

        [JsonPropertyName("resources")]
        public object? Resources { get; set; }

        [JsonPropertyName("prompts")]
        public object? Prompts { get; set; }
    }

    public class ToolsCapability
    {
        [JsonPropertyName("listChanged")]
        public bool? ListChanged { get; set; }
    }

    public class Implementation
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    // Tools list request/response
    public class ListToolsParams
    {
        [JsonPropertyName("cursor")]
        public string? Cursor { get; set; }
    }

    public class ListToolsResult
    {
        [JsonPropertyName("tools")]
        public List<Tool> Tools { get; set; } = new();

        [JsonPropertyName("nextCursor")]
        public string? NextCursor { get; set; }
    }

    public class Tool
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("inputSchema")]
        public ToolInputSchema InputSchema { get; set; } = new();
    }

    public class ToolInputSchema
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "object";

        [JsonPropertyName("properties")]
        public Dictionary<string, object> Properties { get; set; } = new();

        [JsonPropertyName("required")]
        public List<string>? Required { get; set; }
    }

    // Tool call request/response
    public class CallToolParams
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public Dictionary<string, object>? Arguments { get; set; }
    }

    public class CallToolResult
    {
        [JsonPropertyName("content")]
        public List<ToolContent> Content { get; set; } = new();

        [JsonPropertyName("isError")]
        public bool? IsError { get; set; }
    }

    public class ToolContent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
