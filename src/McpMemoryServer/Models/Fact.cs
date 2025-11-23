namespace McpMemoryServer.Models
{
    public class Fact
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Source { get; set; }
        public double Confidence { get; set; } = 1.0;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? Metadata { get; set; }
    }
}
