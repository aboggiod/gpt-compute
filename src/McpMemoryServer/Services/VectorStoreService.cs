using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace McpMemoryServer.Services
{
    /// <summary>
    /// Vector store service using SQLite with simple TF-IDF-based search
    /// For production, consider using ChromaDB, Qdrant, or sqlite-vss extension
    /// </summary>
    public class VectorStoreService : IVectorStoreService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<VectorStoreService> _logger;
        private readonly string _connectionString;

        public VectorStoreService(IConfiguration configuration, ILogger<VectorStoreService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("VectorStore") ?? "Data Source=vectors.db";
        }

        public async Task InitializeAsync()
        {
            try
            {
                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var createTableCmd = connection.CreateCommand();
                createTableCmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS vectors (
                        id TEXT PRIMARY KEY,
                        content TEXT NOT NULL,
                        type TEXT NOT NULL,
                        metadata TEXT,
                        created_at TEXT NOT NULL,
                        updated_at TEXT NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS idx_vectors_type ON vectors(type);
                    CREATE INDEX IF NOT EXISTS idx_vectors_created_at ON vectors(created_at);

                    CREATE VIRTUAL TABLE IF NOT EXISTS vectors_fts USING fts5(
                        id UNINDEXED,
                        content,
                        type UNINDEXED
                    );
                ";
                await createTableCmd.ExecuteNonQueryAsync();

                _logger.LogInformation("Vector store initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize vector store");
                throw;
            }
        }

        public async Task AddTextAsync(string id, string content, string type, Dictionary<string, object>? metadata = null)
        {
            try
            {
                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var now = DateTime.UtcNow.ToString("O");
                var metadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null;

                // Insert or replace in main table
                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO vectors (id, content, type, metadata, created_at, updated_at)
                    VALUES ($id, $content, $type, $metadata, $created_at, $updated_at)
                ";
                cmd.Parameters.AddWithValue("$id", id);
                cmd.Parameters.AddWithValue("$content", content);
                cmd.Parameters.AddWithValue("$type", type);
                cmd.Parameters.AddWithValue("$metadata", (object?)metadataJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$created_at", now);
                cmd.Parameters.AddWithValue("$updated_at", now);
                await cmd.ExecuteNonQueryAsync();

                // Insert into FTS table
                var ftsCmd = connection.CreateCommand();
                ftsCmd.CommandText = @"
                    INSERT OR REPLACE INTO vectors_fts (id, content, type)
                    VALUES ($id, $content, $type)
                ";
                ftsCmd.Parameters.AddWithValue("$id", id);
                ftsCmd.Parameters.AddWithValue("$content", content);
                ftsCmd.Parameters.AddWithValue("$type", type);
                await ftsCmd.ExecuteNonQueryAsync();

                _logger.LogInformation("Added vector {VectorId} of type {Type}", id, type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add vector {VectorId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<VectorSearchResult>> SearchAsync(string query, int topK = 10, string? type = null)
        {
            try
            {
                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                // Use FTS5 for full-text search with BM25 ranking
                var cmd = connection.CreateCommand();
                if (string.IsNullOrEmpty(type))
                {
                    cmd.CommandText = @"
                        SELECT v.id, v.content, v.type, v.metadata, fts.rank
                        FROM vectors_fts fts
                        JOIN vectors v ON fts.id = v.id
                        WHERE vectors_fts MATCH $query
                        ORDER BY rank
                        LIMIT $limit
                    ";
                }
                else
                {
                    cmd.CommandText = @"
                        SELECT v.id, v.content, v.type, v.metadata, fts.rank
                        FROM vectors_fts fts
                        JOIN vectors v ON fts.id = v.id
                        WHERE vectors_fts MATCH $query AND v.type = $type
                        ORDER BY rank
                        LIMIT $limit
                    ";
                    cmd.Parameters.AddWithValue("$type", type);
                }

                cmd.Parameters.AddWithValue("$query", query);
                cmd.Parameters.AddWithValue("$limit", topK);

                var results = new List<VectorSearchResult>();
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var metadataJson = reader.IsDBNull(3) ? null : reader.GetString(3);
                    var metadata = metadataJson != null
                        ? JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson)
                        : null;

                    // Convert BM25 rank (negative value) to similarity score (0-1)
                    var rank = reader.GetDouble(4);
                    var similarity = Math.Max(0, 1.0 / (1.0 - rank)); // Simple normalization

                    results.Add(new VectorSearchResult
                    {
                        Id = reader.GetString(0),
                        Content = reader.GetString(1),
                        Type = reader.GetString(2),
                        Metadata = metadata,
                        Similarity = similarity
                    });
                }

                _logger.LogInformation("Vector search returned {Count} results for query: {Query}", results.Count, query);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search vectors for query: {Query}", query);
                return Enumerable.Empty<VectorSearchResult>();
            }
        }

        public async Task<bool> DeleteAsync(string id)
        {
            try
            {
                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                // Delete from main table
                var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM vectors WHERE id = $id";
                cmd.Parameters.AddWithValue("$id", id);
                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                // Delete from FTS table
                var ftsCmd = connection.CreateCommand();
                ftsCmd.CommandText = "DELETE FROM vectors_fts WHERE id = $id";
                ftsCmd.Parameters.AddWithValue("$id", id);
                await ftsCmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Deleted vector {VectorId}", id);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete vector {VectorId}", id);
                return false;
            }
        }

        public async Task<int> GetCountAsync(string? type = null)
        {
            try
            {
                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var cmd = connection.CreateCommand();
                if (string.IsNullOrEmpty(type))
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM vectors";
                }
                else
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM vectors WHERE type = $type";
                    cmd.Parameters.AddWithValue("$type", type);
                }

                var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
                return (int)count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get vector count");
                return 0;
            }
        }
    }
}
