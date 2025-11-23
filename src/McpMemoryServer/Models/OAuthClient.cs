namespace McpMemoryServer.Models
{
    public class OAuthClient
    {
        public int Id { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Scopes { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }
}
