namespace McpMemoryServer.Models
{
    public class Conversation
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? Metadata { get; set; }

        // Navigation property
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
