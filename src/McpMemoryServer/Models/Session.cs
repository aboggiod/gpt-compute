namespace McpMemoryServer.Models
{
    public class Session
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string? ClientInfo { get; set; }
        public string? Capabilities { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? Metadata { get; set; }
    }
}
