namespace McpMemoryServer.Services
{
    public class VectorSearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "message", "fact", "document"
        public double Similarity { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public interface IVectorStoreService
    {
        Task InitializeAsync();
        Task AddTextAsync(string id, string content, string type, Dictionary<string, object>? metadata = null);
        Task<IEnumerable<VectorSearchResult>> SearchAsync(string query, int topK = 10, string? type = null);
        Task<bool> DeleteAsync(string id);
        Task<int> GetCountAsync(string? type = null);
    }
}
