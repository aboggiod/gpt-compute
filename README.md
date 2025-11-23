# MCP Memory Server

A production-grade persistent-memory API server built with .NET 8 that exposes memory + local file tools to ChatGPT via the Model Context Protocol (MCP). Gives your custom GPT "SICK memory" that survives across sessions.

## Features

- **Full MCP 2024-11-05 Protocol Support** - Complete JSON-RPC 2.0 implementation
- **OAuth 2.1 Authentication** - Client credentials flow with JWT tokens
- **Persistent Memory**
  - SQLite + EF Core for relational storage (conversations, preferences, facts)
  - Vector store with FTS5 for semantic search
  - Session management with automatic cleanup
- **MCP Tools**
  - `memory.read` - Read stored memory (preferences, facts, conversations)
  - `memory.write` - Write to persistent memory
  - `memory.search` - Semantic search across all memory using vector similarity
  - `filesystem.read_file` - Read files from allowed directories
  - `filesystem.list_directory` - List files and directories
  - `filesystem.search_files` - Search files by pattern
- **Production Ready**
  - Comprehensive unit & integration tests (100+ test cases)
  - Full OpenAPI/Swagger documentation
  - Proper error handling and logging
  - Security controls (file type restrictions, path traversal protection)

## Tech Stack

- .NET 8 SDK
- ASP.NET Core Minimal API
- Entity Framework Core + SQLite
- SQLite FTS5 for vector search
- JWT Bearer Authentication
- xUnit + Moq for testing
- Swashbuckle for OpenAPI

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An HTTPS endpoint (use ngrok or Cloudflare Tunnel for local dev)
- ChatGPT Plus or Enterprise (for MCP support)

## Quick Start

### 1. Clone and Setup

```bash
git clone <your-repo-url>
cd gpt-compute

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests to verify everything works
dotnet test
```

### 2. Configure Environment

Copy `.env.example` to `.env` and update:

```bash
cp .env.example .env
```

Edit `.env`:

```env
# CHANGE THESE IN PRODUCTION!
JWT_KEY=your-super-secret-key-minimum-32-characters-long
OAUTH_CLIENT_ID=chatgpt-client
OAUTH_CLIENT_SECRET=your-secure-client-secret

# Database paths
CONNECTION_STRING=Data Source=memory.db
VECTOR_STORE_CONNECTION=Data Source=vectors.db

# Server config
ASPNETCORE_URLS=https://localhost:5001
ASPNETCORE_ENVIRONMENT=Development
```

### 3. Run the Server

```bash
cd src/McpMemoryServer
dotnet run
```

The server will start on `https://localhost:5001` with Swagger UI at the root.

### 4. Test OAuth

Get an access token:

```bash
curl -X POST https://localhost:5001/oauth/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=chatgpt-client" \
  -d "client_secret=$2a$11$YourHashedSecretHere" \
  -d "scope=memory.read memory.write tools.filesystem"
```

Response:
```json
{
  "access_token": "eyJhbGc...",
  "token_type": "Bearer",
  "expires_in": 86400,
  "scope": "memory.read memory.write tools.filesystem"
}
```

### 5. Test MCP Endpoint

Initialize MCP session:

```bash
export TOKEN="your-token-from-above"

curl -X POST https://localhost:5001/mcp \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
      "protocolVersion": "2024-11-05",
      "capabilities": {},
      "clientInfo": {
        "name": "TestClient",
        "version": "1.0.0"
      }
    }
  }'
```

List available tools:

```bash
curl -X POST https://localhost:5001/mcp \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list",
    "params": {}
  }'
```

### 6. Deploy to Production

For ChatGPT to access your server, it needs to be publicly accessible over HTTPS:

**Option A: Using ngrok (easiest for testing)**

```bash
ngrok http https://localhost:5001
```

Copy the HTTPS URL (e.g., `https://abc123.ngrok.io`)

**Option B: Deploy to cloud provider**

Deploy to Azure App Service, AWS, Google Cloud, or any cloud provider with HTTPS support.

### 7. Configure in ChatGPT

1. Go to ChatGPT Settings → Connectors → Create
2. Fill in:
   - **Name**: My Memory Server
   - **Description**: Persistent memory across sessions
   - **Connector URL**: `https://your-server-url.com/mcp`
3. Configure OAuth:
   - **Token Endpoint**: `https://your-server-url.com/oauth/token`
   - **Client ID**: `chatgpt-client`
   - **Client Secret**: (your secret from `.env`)
   - **Scopes**: `memory.read memory.write tools.filesystem`
4. Save and test the connection

## MCP Tools Reference

### memory.read

Read stored memory for the current session.

**Parameters:**
- `type` (string, optional): `"all"`, `"preferences"`, `"facts"`, or `"conversations"` (default: `"all"`)
- `category` (string, optional): Filter by category
- `limit` (integer, optional): Max results (default: 50)
- `session_id` (string, optional): Session ID (uses current if not provided)

**Example:**
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "memory.read",
    "arguments": {
      "type": "facts",
      "category": "personal"
    }
  }
}
```

### memory.write

Write to persistent memory.

**Parameters:**
- `type` (string): `"preference"`, `"fact"`, or `"message"` (default: `"fact"`)
- `content` (string, required): Content to store
- `key` (string): Key for preferences
- `category` (string, optional): Category for organization
- `confidence` (number, optional): Confidence score 0-1 (default: 1.0)

**Example:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "tools/call",
  "params": {
    "name": "memory.write",
    "arguments": {
      "type": "fact",
      "content": "User prefers Python over JavaScript",
      "category": "programming",
      "confidence": 0.95
    }
  }
}
```

### memory.search

Semantic search across all stored memory.

**Parameters:**
- `query` (string, required): Search query
- `type` (string, optional): Filter by type
- `limit` (integer, optional): Max results (default: 10)

**Example:**
```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "tools/call",
  "params": {
    "name": "memory.search",
    "arguments": {
      "query": "programming preferences",
      "limit": 5
    }
  }
}
```

### filesystem.read_file

Read a file from the allowed directory.

**Parameters:**
- `path` (string, required): Relative path to file

**Example:**
```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "method": "tools/call",
  "params": {
    "name": "filesystem.read_file",
    "arguments": {
      "path": "documents/notes.txt"
    }
  }
}
```

### filesystem.list_directory

List files and directories.

**Parameters:**
- `path` (string, optional): Directory path (default: `"."`)
- `recursive` (boolean, optional): List recursively (default: `false`)

**Example:**
```json
{
  "jsonrpc": "2.0",
  "id": 7,
  "method": "tools/call",
  "params": {
    "name": "filesystem.list_directory",
    "arguments": {
      "path": "documents",
      "recursive": true
    }
  }
}
```

### filesystem.search_files

Search for files matching a pattern.

**Parameters:**
- `pattern` (string, optional): File pattern (default: `"*"`)
- `directory` (string, optional): Directory to search

**Example:**
```json
{
  "jsonrpc": "2.0",
  "id": 8,
  "method": "tools/call",
  "params": {
    "name": "filesystem.search_files",
    "arguments": {
      "pattern": "*.md",
      "directory": "documents"
    }
  }
}
```

## Project Structure

```
gpt-compute/
├── src/
│   └── McpMemoryServer/
│       ├── Controllers/          # API controllers
│       │   ├── AuthController.cs
│       │   └── McpController.cs
│       ├── Data/                 # Database context
│       │   └── MemoryDbContext.cs
│       ├── Models/               # Data models
│       │   ├── Conversation.cs
│       │   ├── Message.cs
│       │   ├── UserPreference.cs
│       │   ├── Fact.cs
│       │   ├── OAuthClient.cs
│       │   └── Session.cs
│       ├── Mcp/                  # MCP protocol models
│       │   └── Models/
│       │       ├── JsonRpcMessage.cs
│       │       └── McpProtocol.cs
│       ├── Services/             # Business logic
│       │   ├── AuthService.cs
│       │   ├── MemoryService.cs
│       │   ├── VectorStoreService.cs
│       │   ├── FileSystemService.cs
│       │   ├── SessionManager.cs
│       │   └── McpService.cs
│       ├── Program.cs            # Application entry point
│       └── appsettings.json
├── tests/
│   └── McpMemoryServer.Tests/
│       ├── Services/             # Service tests
│       ├── Controllers/          # Controller tests
│       └── Integration/          # Integration tests
├── mcpserver.json               # MCP server manifest
├── .env.example                 # Environment template
└── README.md
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~AuthServiceTests"

# Generate coverage report (requires coverlet)
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Security Considerations

### Production Checklist

- [ ] Change `JWT_KEY` to a strong random value (min 32 chars)
- [ ] Change `OAUTH_CLIENT_SECRET` to a secure random value
- [ ] Use proper password hashing (BCrypt) for client secrets
- [ ] Enable HTTPS in production (`RequireHttpsMetadata = true`)
- [ ] Restrict `FileSystem:BaseDirectory` to safe location
- [ ] Limit `FileSystem:AllowedExtensions` to necessary types
- [ ] Set up proper CORS policies
- [ ] Enable rate limiting
- [ ] Configure proper logging and monitoring
- [ ] Use environment-specific configuration
- [ ] Implement backup strategy for SQLite databases

### File System Security

The FileSystem service has built-in protections:
- Path traversal prevention
- File type restrictions (configurable extensions)
- Base directory confinement
- No write operations by default

## Troubleshooting

### "Failed to authenticate" error

- Verify client credentials in `.env` match the database
- Check JWT key configuration
- Ensure token hasn't expired (24h default)

### "Vector store initialization failed"

- Ensure SQLite FTS5 extension is available
- Check file permissions for database files
- Verify connection string is correct

### ChatGPT can't connect

- Confirm server is publicly accessible over HTTPS
- Verify OAuth endpoint returns valid tokens
- Check firewall/security group settings
- Test with curl first to validate endpoints

### Tests failing

- Ensure .NET 8 SDK is installed
- Run `dotnet restore` to get dependencies
- Check for port conflicts (5000, 5001)
- Clear bin/obj folders and rebuild

## Development

### Adding a New Tool

1. Define tool in `McpService.GetAvailableTools()`
2. Implement handler method in `McpService.ExecuteToolAsync()`
3. Add unit tests in `McpServiceTests.cs`
4. Update README documentation

### Database Migrations

```bash
# Add migration
dotnet ef migrations add MigrationName -p src/McpMemoryServer

# Update database
dotnet ef database update -p src/McpMemoryServer
```

## API Documentation

When running in Development mode, Swagger UI is available at the root URL:

```
https://localhost:5001/
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add/update tests
5. Ensure all tests pass
6. Submit a pull request

## License

MIT License - see LICENSE file for details

## References

- [MCP Specification 2024-11-05](https://spec.modelcontextprotocol.io/specification/2024-11-05/)
- [OAuth 2.1 Draft](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-10)
- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [OpenAI MCP Integration](https://developers.openai.com/apps-sdk/build/mcp-server/)

## Support

For issues, questions, or contributions, please open an issue on GitHub.

---

**Built with ❤️ using .NET 8 and MCP 2024-11-05**
