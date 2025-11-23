namespace McpMemoryServer.Services
{
    public class FileInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }

    public interface IFileSystemService
    {
        Task<string> ReadFileAsync(string path);
        Task<IEnumerable<FileInfo>> ListDirectoryAsync(string path, bool recursive = false);
        Task<IEnumerable<FileInfo>> SearchFilesAsync(string pattern, string? directory = null);
        Task<bool> FileExistsAsync(string path);
        Task<bool> DirectoryExistsAsync(string path);
    }
}
