namespace McpMemoryServer.Services
{
    public class FileSystemService : IFileSystemService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FileSystemService> _logger;
        private readonly string _baseDirectory;
        private readonly HashSet<string> _allowedExtensions;

        public FileSystemService(IConfiguration configuration, ILogger<FileSystemService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Security: Restrict to specific base directory
            _baseDirectory = configuration["FileSystem:BaseDirectory"] ?? Path.Combine(Directory.GetCurrentDirectory(), "data");

            // Ensure base directory exists
            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
            }

            // Allowed file extensions for security
            var allowedExts = configuration["FileSystem:AllowedExtensions"] ?? ".txt,.md,.json,.xml,.csv,.log";
            _allowedExtensions = new HashSet<string>(
                allowedExts.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(ext => ext.Trim().ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase
            );

            _logger.LogInformation("FileSystemService initialized with base directory: {BaseDirectory}", _baseDirectory);
        }

        public async Task<string> ReadFileAsync(string path)
        {
            try
            {
                var fullPath = GetSafePath(path);

                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"File not found: {path}");
                }

                // Check file extension
                var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                if (!_allowedExtensions.Contains(extension))
                {
                    throw new UnauthorizedAccessException($"File type not allowed: {extension}");
                }

                _logger.LogInformation("Reading file: {FilePath}", path);
                return await File.ReadAllTextAsync(fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read file: {FilePath}", path);
                throw;
            }
        }

        public async Task<IEnumerable<FileInfo>> ListDirectoryAsync(string path, bool recursive = false)
        {
            try
            {
                var fullPath = GetSafePath(path);

                if (!Directory.Exists(fullPath))
                {
                    throw new DirectoryNotFoundException($"Directory not found: {path}");
                }

                var fileInfos = new List<FileInfo>();
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                // Get directories
                await Task.Run(() =>
                {
                    foreach (var dir in Directory.GetDirectories(fullPath, "*", searchOption))
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        fileInfos.Add(new FileInfo
                        {
                            Name = dirInfo.Name,
                            Path = GetRelativePath(dir),
                            IsDirectory = true,
                            Size = 0,
                            LastModified = dirInfo.LastWriteTimeUtc
                        });
                    }

                    // Get files
                    foreach (var file in Directory.GetFiles(fullPath, "*", searchOption))
                    {
                        var fileInfo = new System.IO.FileInfo(file);
                        var extension = fileInfo.Extension.ToLowerInvariant();

                        // Only include allowed file types
                        if (_allowedExtensions.Contains(extension))
                        {
                            fileInfos.Add(new FileInfo
                            {
                                Name = fileInfo.Name,
                                Path = GetRelativePath(file),
                                IsDirectory = false,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTimeUtc
                            });
                        }
                    }
                });

                _logger.LogInformation("Listed {Count} items in directory: {DirectoryPath}", fileInfos.Count, path);
                return fileInfos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list directory: {DirectoryPath}", path);
                throw;
            }
        }

        public async Task<IEnumerable<FileInfo>> SearchFilesAsync(string pattern, string? directory = null)
        {
            try
            {
                var searchDir = string.IsNullOrEmpty(directory) ? _baseDirectory : GetSafePath(directory);

                if (!Directory.Exists(searchDir))
                {
                    throw new DirectoryNotFoundException($"Directory not found: {directory ?? "base"}");
                }

                var fileInfos = new List<FileInfo>();

                await Task.Run(() =>
                {
                    foreach (var file in Directory.GetFiles(searchDir, pattern, SearchOption.AllDirectories))
                    {
                        var fileInfo = new System.IO.FileInfo(file);
                        var extension = fileInfo.Extension.ToLowerInvariant();

                        // Only include allowed file types
                        if (_allowedExtensions.Contains(extension))
                        {
                            fileInfos.Add(new FileInfo
                            {
                                Name = fileInfo.Name,
                                Path = GetRelativePath(file),
                                IsDirectory = false,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTimeUtc
                            });
                        }
                    }
                });

                _logger.LogInformation("Found {Count} files matching pattern: {Pattern}", fileInfos.Count, pattern);
                return fileInfos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search files with pattern: {Pattern}", pattern);
                throw;
            }
        }

        public Task<bool> FileExistsAsync(string path)
        {
            try
            {
                var fullPath = GetSafePath(path);
                return Task.FromResult(File.Exists(fullPath));
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> DirectoryExistsAsync(string path)
        {
            try
            {
                var fullPath = GetSafePath(path);
                return Task.FromResult(Directory.Exists(fullPath));
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        private string GetSafePath(string relativePath)
        {
            // Remove leading slash if present
            relativePath = relativePath.TrimStart('/', '\\');

            // Combine with base directory
            var fullPath = Path.Combine(_baseDirectory, relativePath);

            // Normalize the path
            fullPath = Path.GetFullPath(fullPath);

            // Security: Ensure the path is within the base directory
            if (!fullPath.StartsWith(_baseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Access to path outside base directory is not allowed");
            }

            return fullPath;
        }

        private string GetRelativePath(string fullPath)
        {
            return Path.GetRelativePath(_baseDirectory, fullPath);
        }
    }
}
