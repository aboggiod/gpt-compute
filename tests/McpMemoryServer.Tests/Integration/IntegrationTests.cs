using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using McpMemoryServer.Data;
using McpMemoryServer.Mcp.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace McpMemoryServer.Tests.Integration
{
    public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public IntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the existing DbContext
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<MemoryDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add in-memory database for testing
                    services.AddDbContext<MemoryDbContext>(options =>
                    {
                        options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
                    });
                });
            });

            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task OAuth_Token_ValidCredentials_ReturnsToken()
        {
            // Arrange
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", "chatgpt-client"),
                new KeyValuePair<string, string>("client_secret", "$2a$11$YourHashedSecretHere"),
                new KeyValuePair<string, string>("scope", "memory.read memory.write")
            });

            // Act
            var response = await _client.PostAsync("/oauth/token", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseBody);
            Assert.NotNull(tokenResponse);
            Assert.True(tokenResponse.ContainsKey("access_token"));
        }

        [Fact]
        public async Task Mcp_Health_ReturnsHealthy()
        {
            // Act
            var response = await _client.GetAsync("/mcp/health");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("healthy", body);
        }

        [Fact]
        public async Task Mcp_Info_ReturnsServerInfo()
        {
            // Act
            var response = await _client.GetAsync("/mcp/info");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("McpMemoryServer", body);
            Assert.Contains("2024-11-05", body);
        }

        [Fact]
        public async Task Mcp_Initialize_WithAuth_ReturnsSessionId()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 1,
                Method = "initialize",
                Params = new InitializeParams
                {
                    ProtocolVersion = "2024-11-05",
                    ClientInfo = new Implementation { Name = "TestClient", Version = "1.0.0" }
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = content
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.SendAsync(httpRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains("sessionId", responseBody);
        }

        [Fact]
        public async Task Mcp_ToolsList_WithAuth_ReturnsTools()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 2,
                Method = "tools/list",
                Params = new { }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = content
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.SendAsync(httpRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains("memory.read", responseBody);
            Assert.Contains("memory.write", responseBody);
            Assert.Contains("filesystem.read_file", responseBody);
        }

        [Fact]
        public async Task Mcp_Unauthorized_WithoutAuth_Returns401()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 1,
                Method = "tools/list",
                Params = new { }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/mcp", content);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        private async Task<string> GetAuthTokenAsync()
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", "chatgpt-client"),
                new KeyValuePair<string, string>("client_secret", "$2a$11$YourHashedSecretHere"),
                new KeyValuePair<string, string>("scope", "memory.read memory.write tools.filesystem")
            });

            var response = await _client.PostAsync("/oauth/token", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseBody);

            return tokenResponse?["access_token"].GetString() ?? throw new Exception("Failed to get token");
        }
    }
}
