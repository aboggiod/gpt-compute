namespace McpMemoryServer.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public string Role { get; set; } = string.Empty; // "user", "assistant", "system"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Metadata { get; set; }

        // Navigation property
        public virtual Conversation Conversation { get; set; } = null!;
    }
}
