using System.Text.Json;
using McpMemoryServer.Mcp.Models;

namespace McpMemoryServer.Services
{
    public class McpService : IMcpService
    {
        private readonly IMemoryService _memoryService;
        private readonly IVectorStoreService _vectorStore;
        private readonly IFileSystemService _fileSystem;
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<McpService> _logger;

        public McpService(
            IMemoryService memoryService,
            IVectorStoreService vectorStore,
            IFileSystemService fileSystem,
            ISessionManager sessionManager,
            ILogger<McpService> logger)
        {
            _memoryService = memoryService;
            _vectorStore = vectorStore;
            _fileSystem = fileSystem;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        public async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request, string? sessionId = null)
        {
            try
            {
                _logger.LogInformation("Handling MCP request: {Method} (ID: {RequestId})", request.Method, request.Id);

                return request.Method switch
                {
                    "initialize" => await HandleInitializeAsync(request),
                    "tools/list" => HandleToolsList(request),
                    "tools/call" => await HandleToolCallAsync(request, sessionId),
                    _ => new JsonRpcResponse
                    {
                        Id = request.Id,
                        Error = new JsonRpcError
                        {
                            Code = JsonRpcErrorCodes.MethodNotFound,
                            Message = $"Method not found: {request.Method}"
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling MCP request: {Method}", request.Method);
                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.InternalError,
                        Message = ex.Message,
                        Data = new { exception = ex.GetType().Name }
                    }
                };
            }
        }

        private async Task<JsonRpcResponse> HandleInitializeAsync(JsonRpcRequest request)
        {
            try
            {
                var paramsJson = JsonSerializer.Serialize(request.Params);
                var initParams = JsonSerializer.Deserialize<InitializeParams>(paramsJson);

                // Create a new session
                var clientInfo = JsonSerializer.Serialize(initParams?.ClientInfo);
                var capabilities = JsonSerializer.Serialize(initParams?.Capabilities);
                var session = await _sessionManager.CreateSessionAsync(clientInfo, capabilities);

                var result = GetInitializeResult();

                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Result = new
                    {
                        result.ProtocolVersion,
                        result.Capabilities,
                        result.ServerInfo,
                        sessionId = session.SessionId // Custom: return session ID
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in initialize");
                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.InternalError,
                        Message = $"Initialization failed: {ex.Message}"
                    }
                };
            }
        }

        private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
        {
            var tools = GetAvailableTools();

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = new ListToolsResult
                {
                    Tools = tools
                }
            };
        }

        private async Task<JsonRpcResponse> HandleToolCallAsync(JsonRpcRequest request, string? sessionId)
        {
            try
            {
                var paramsJson = JsonSerializer.Serialize(request.Params);
                var callParams = JsonSerializer.Deserialize<CallToolParams>(paramsJson);

                if (callParams == null || string.IsNullOrEmpty(callParams.Name))
                {
                    return new JsonRpcResponse
                    {
                        Id = request.Id,
                        Error = new JsonRpcError
                        {
                            Code = JsonRpcErrorCodes.InvalidParams,
                            Message = "Tool name is required"
                        }
                    };
                }

                // Use session ID from params or header
                var effectiveSessionId = GetArgumentValue<string>(callParams.Arguments, "session_id") ?? sessionId ?? "default";

                // Validate session
                if (effectiveSessionId != "default" && !await _sessionManager.IsSessionValidAsync(effectiveSessionId))
                {
                    return new JsonRpcResponse
                    {
                        Id = request.Id,
                        Error = new JsonRpcError
                        {
                            Code = JsonRpcErrorCodes.InvalidParams,
                            Message = "Invalid or expired session"
                        }
                    };
                }

                // Update session access time
                if (effectiveSessionId != "default")
                {
                    await _sessionManager.UpdateSessionAccessAsync(effectiveSessionId);
                }

                var result = await ExecuteToolAsync(callParams.Name, callParams.Arguments ?? new(), effectiveSessionId);

                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Result = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool");
                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Result = new CallToolResult
                    {
                        Content = new List<ToolContent>
                        {
                            new() { Type = "text", Text = $"Error: {ex.Message}" }
                        },
                        IsError = true
                    }
                };
            }
        }

        private async Task<CallToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> args, string sessionId)
        {
            _logger.LogInformation("Executing tool: {ToolName} for session {SessionId}", toolName, sessionId);

            try
            {
                return toolName switch
                {
                    "memory.read" => await MemoryReadAsync(args, sessionId),
                    "memory.write" => await MemoryWriteAsync(args, sessionId),
                    "memory.search" => await MemorySearchAsync(args, sessionId),
                    "filesystem.read_file" => await FileSystemReadFileAsync(args),
                    "filesystem.list_directory" => await FileSystemListDirectoryAsync(args),
                    "filesystem.search_files" => await FileSystemSearchFilesAsync(args),
                    _ => new CallToolResult
                    {
                        Content = new List<ToolContent>
                        {
                            new() { Type = "text", Text = $"Unknown tool: {toolName}" }
                        },
                        IsError = true
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tool execution: {ToolName}", toolName);
                return new CallToolResult
                {
                    Content = new List<ToolContent>
                    {
                        new() { Type = "text", Text = $"Tool execution failed: {ex.Message}" }
                    },
                    IsError = true
                };
            }
        }

        #region Memory Tools

        private async Task<CallToolResult> MemoryReadAsync(Dictionary<string, object> args, string sessionId)
        {
            var type = GetArgumentValue<string>(args, "type") ?? "all";
            var category = GetArgumentValue<string>(args, "category");
            var limit = GetArgumentValue<int>(args, "limit", 50);

            var results = new List<string>();

            if (type == "all" || type == "preferences")
            {
                var prefs = await _memoryService.GetPreferencesAsync(sessionId, category);
                results.AddRange(prefs.Select(p => $"Preference: {p.Key} = {p.Value} (Category: {p.Category})"));
            }

            if (type == "all" || type == "facts")
            {
                var facts = await _memoryService.GetFactsAsync(sessionId, category, take: limit);
                results.AddRange(facts.Select(f => $"Fact: {f.Content} (Category: {f.Category}, Confidence: {f.Confidence})"));
            }

            if (type == "all" || type == "conversations")
            {
                var conversations = await _memoryService.GetConversationsAsync(sessionId, take: limit);
                foreach (var conv in conversations)
                {
                    var messages = await _memoryService.GetMessagesAsync(conv.Id);
                    results.Add($"Conversation: {conv.Title} ({messages.Count()} messages)");
                }
            }

            var content = results.Any()
                ? string.Join("\n", results)
                : "No memory entries found.";

            return new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new() { Type = "text", Text = content }
                }
            };
        }

        private async Task<CallToolResult> MemoryWriteAsync(Dictionary<string, object> args, string sessionId)
        {
            var type = GetArgumentValue<string>(args, "type") ?? "fact";
            var content = GetArgumentValue<string>(args, "content");
            var category = GetArgumentValue<string>(args, "category");

            if (string.IsNullOrEmpty(content))
            {
                return new CallToolResult
                {
                    Content = new List<ToolContent>
                    {
                        new() { Type = "text", Text = "Error: content is required" }
                    },
                    IsError = true
                };
            }

            string result;

            switch (type.ToLower())
            {
                case "preference":
                    var key = GetArgumentValue<string>(args, "key");
                    if (string.IsNullOrEmpty(key))
                    {
                        return new CallToolResult
                        {
                            Content = new List<ToolContent>
                            {
                                new() { Type = "text", Text = "Error: key is required for preferences" }
                            },
                            IsError = true
                        };
                    }
                    var pref = await _memoryService.SetPreferenceAsync(sessionId, key, content, category);
                    result = $"Preference saved: {pref.Key} = {pref.Value}";

                    // Also add to vector store
                    await _vectorStore.AddTextAsync($"pref_{pref.Id}", $"{key}: {content}", "preference",
                        new Dictionary<string, object> { ["session_id"] = sessionId, ["category"] = category ?? "general" });
                    break;

                case "fact":
                    var confidence = GetArgumentValue<double>(args, "confidence", 1.0);
                    var fact = await _memoryService.CreateFactAsync(sessionId, content, category, confidence: confidence);
                    result = $"Fact saved: {fact.Content} (ID: {fact.Id})";

                    // Add to vector store
                    await _vectorStore.AddTextAsync($"fact_{fact.Id}", content, "fact",
                        new Dictionary<string, object> { ["session_id"] = sessionId, ["category"] = category ?? "general" });
                    break;

                case "message":
                    var conversationId = GetArgumentValue<int>(args, "conversation_id");
                    var role = GetArgumentValue<string>(args, "role") ?? "user";

                    // Create conversation if ID not provided
                    if (conversationId == 0)
                    {
                        var title = GetArgumentValue<string>(args, "title") ?? "New Conversation";
                        var conv = await _memoryService.CreateConversationAsync(sessionId, title);
                        conversationId = conv.Id;
                    }

                    var message = await _memoryService.AddMessageAsync(conversationId, role, content);
                    result = $"Message saved to conversation {conversationId}";

                    // Add to vector store
                    await _vectorStore.AddTextAsync($"message_{message.Id}", content, "message",
                        new Dictionary<string, object>
                        {
                            ["session_id"] = sessionId,
                            ["conversation_id"] = conversationId,
                            ["role"] = role
                        });
                    break;

                default:
                    return new CallToolResult
                    {
                        Content = new List<ToolContent>
                        {
                            new() { Type = "text", Text = $"Error: unknown type '{type}'. Use: preference, fact, or message" }
                        },
                        IsError = true
                    };
            }

            return new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new() { Type = "text", Text = result }
                }
            };
        }

        private async Task<CallToolResult> MemorySearchAsync(Dictionary<string, object> args, string sessionId)
        {
            var query = GetArgumentValue<string>(args, "query");
            var type = GetArgumentValue<string>(args, "type");
            var limit = GetArgumentValue<int>(args, "limit", 10);

            if (string.IsNullOrEmpty(query))
            {
                return new CallToolResult
                {
                    Content = new List<ToolContent>
                    {
                        new() { Type = "text", Text = "Error: query is required" }
                    },
                    IsError = true
                };
            }

            var searchResults = await _vectorStore.SearchAsync(query, limit, type);

            if (!searchResults.Any())
            {
                return new CallToolResult
                {
                    Content = new List<ToolContent>
                    {
                        new() { Type = "text", Text = $"No results found for query: {query}" }
                    }
                };
            }

            var results = searchResults.Select(r =>
                $"[{r.Type}] {r.Content} (Similarity: {r.Similarity:F2})");

            return new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new() { Type = "text", Text = string.Join("\n\n", results) }
                }
            };
        }

        #endregion

        #region Filesystem Tools

        private async Task<CallToolResult> FileSystemReadFileAsync(Dictionary<string, object> args)
        {
            var path = GetArgumentValue<string>(args, "path");

            if (string.IsNullOrEmpty(path))
            {
                return new CallToolResult
                {
                    Content = new List<ToolContent>
                    {
                        new() { Type = "text", Text = "Error: path is required" }
                    },
                    IsError = true
                };
            }

            var content = await _fileSystem.ReadFileAsync(path);

            return new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new() { Type = "text", Text = content }
                }
            };
        }

        private async Task<CallToolResult> FileSystemListDirectoryAsync(Dictionary<string, object> args)
        {
            var path = GetArgumentValue<string>(args, "path") ?? ".";
            var recursive = GetArgumentValue<bool>(args, "recursive", false);

            var items = await _fileSystem.ListDirectoryAsync(path, recursive);

            var result = string.Join("\n", items.Select(i =>
                $"{(i.IsDirectory ? "[DIR]" : "[FILE]")} {i.Path} ({i.Size} bytes, modified: {i.LastModified:yyyy-MM-dd HH:mm})"));

            return new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new() { Type = "text", Text = result }
                }
            };
        }

        private async Task<CallToolResult> FileSystemSearchFilesAsync(Dictionary<string, object> args)
        {
            var pattern = GetArgumentValue<string>(args, "pattern") ?? "*";
            var directory = GetArgumentValue<string>(args, "directory");

            var files = await _fileSystem.SearchFilesAsync(pattern, directory);

            var result = string.Join("\n", files.Select(f =>
                $"{f.Path} ({f.Size} bytes, modified: {f.LastModified:yyyy-MM-dd HH:mm})"));

            if (string.IsNullOrEmpty(result))
            {
                result = $"No files found matching pattern: {pattern}";
            }

            return new CallToolResult
            {
                Content = new List<ToolContent>
                {
                    new() { Type = "text", Text = result }
                }
            };
        }

        #endregion

        public InitializeResult GetInitializeResult()
        {
            return new InitializeResult
            {
                ProtocolVersion = "2024-11-05",
                Capabilities = new ServerCapabilities
                {
                    Tools = new ToolsCapability { ListChanged = false }
                },
                ServerInfo = new Implementation
                {
                    Name = "McpMemoryServer",
                    Version = "1.0.0"
                }
            };
        }

        public List<Tool> GetAvailableTools()
        {
            return new List<Tool>
            {
                new()
                {
                    Name = "memory.read",
                    Description = "Read stored memory (preferences, facts, conversations) for the current session",
                    InputSchema = new ToolInputSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, object>
                        {
                            ["type"] = new { type = "string", description = "Type of memory to read: 'all', 'preferences', 'facts', 'conversations'", @default = "all" },
                            ["category"] = new { type = "string", description = "Filter by category (optional)" },
                            ["limit"] = new { type = "integer", description = "Maximum number of results", @default = 50 },
                            ["session_id"] = new { type = "string", description = "Session ID (optional, uses current session if not provided)" }
                        }
                    }
                },
                new()
                {
                    Name = "memory.write",
                    Description = "Write to persistent memory (preferences, facts, messages)",
                    InputSchema = new ToolInputSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, object>
                        {
                            ["type"] = new { type = "string", description = "Type: 'preference', 'fact', 'message'", @default = "fact" },
                            ["content"] = new { type = "string", description = "Content to store" },
                            ["key"] = new { type = "string", description = "Key (required for preferences)" },
                            ["category"] = new { type = "string", description = "Category for organization" },
                            ["confidence"] = new { type = "number", description = "Confidence score for facts (0-1)", @default = 1.0 },
                            ["conversation_id"] = new { type = "integer", description = "Conversation ID for messages" },
                            ["role"] = new { type = "string", description = "Role for messages: 'user', 'assistant', 'system'" },
                            ["session_id"] = new { type = "string", description = "Session ID (optional)" }
                        },
                        Required = new List<string> { "content" }
                    }
                },
                new()
                {
                    Name = "memory.search",
                    Description = "Semantic search across all stored memory using vector similarity",
                    InputSchema = new ToolInputSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, object>
                        {
                            ["query"] = new { type = "string", description = "Search query" },
                            ["type"] = new { type = "string", description = "Filter by type: 'preference', 'fact', 'message'" },
                            ["limit"] = new { type = "integer", description = "Maximum results to return", @default = 10 },
                            ["session_id"] = new { type = "string", description = "Session ID (optional)" }
                        },
                        Required = new List<string> { "query" }
                    }
                },
                new()
                {
                    Name = "filesystem.read_file",
                    Description = "Read the contents of a file from the allowed directory",
                    InputSchema = new ToolInputSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, object>
                        {
                            ["path"] = new { type = "string", description = "Relative path to the file" }
                        },
                        Required = new List<string> { "path" }
                    }
                },
                new()
                {
                    Name = "filesystem.list_directory",
                    Description = "List files and directories in a directory",
                    InputSchema = new ToolInputSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, object>
                        {
                            ["path"] = new { type = "string", description = "Relative path to the directory", @default = "." },
                            ["recursive"] = new { type = "boolean", description = "List recursively", @default = false }
                        }
                    }
                },
                new()
                {
                    Name = "filesystem.search_files",
                    Description = "Search for files matching a pattern",
                    InputSchema = new ToolInputSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, object>
                        {
                            ["pattern"] = new { type = "string", description = "File pattern to match (e.g., '*.txt')", @default = "*" },
                            ["directory"] = new { type = "string", description = "Directory to search in (optional)" }
                        }
                    }
                }
            };
        }

        private T? GetArgumentValue<T>(Dictionary<string, object>? args, string key, T? defaultValue = default)
        {
            if (args == null || !args.TryGetValue(key, out var value))
            {
                return defaultValue;
            }

            try
            {
                if (value is JsonElement jsonElement)
                {
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
