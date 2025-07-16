using VideoConversion.Models;

namespace VideoConversion.Services
{
    /// <summary>
    /// 文件管理服务
    /// </summary>
    public class FileService
    {
        private readonly ILogger<FileService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _uploadPath;
        private readonly string _outputPath;
        private readonly long _maxFileSize;
        private readonly string[] _allowedExtensions;

        public FileService(ILogger<FileService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            _uploadPath = _configuration.GetValue<string>("VideoConversion:UploadPath") ?? "uploads";
            _outputPath = _configuration.GetValue<string>("VideoConversion:OutputPath") ?? "outputs";
            _maxFileSize = _configuration.GetValue<long>("VideoConversion:MaxFileSize", 2147483648); // 2GB
            _allowedExtensions = _configuration.GetSection("VideoConversion:AllowedExtensions").Get<string[]>() 
                ?? new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".3gp" };

            // 确保目录存在
            EnsureDirectoriesExist();
        }

        /// <summary>
        /// 确保必要的目录存在
        /// </summary>
        private void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(_uploadPath))
                {
                    Directory.CreateDirectory(_uploadPath);
                    _logger.LogInformation("创建上传目录: {UploadPath}", _uploadPath);
                }

                if (!Directory.Exists(_outputPath))
                {
                    Directory.CreateDirectory(_outputPath);
                    _logger.LogInformation("创建输出目录: {OutputPath}", _outputPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建目录失败");
                throw;
            }
        }

        /// <summary>
        /// 验证上传文件
        /// </summary>
        public (bool IsValid, string ErrorMessage) ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return (false, "请选择一个文件");
            }

            if (file.Length > _maxFileSize)
            {
                var maxSizeMB = _maxFileSize / (1024 * 1024);
                return (false, $"文件大小超过限制 ({maxSizeMB}MB)");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                var allowedFormats = string.Join(", ", _allowedExtensions);
                return (false, $"不支持的文件格式。支持的格式: {allowedFormats}");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// 保存上传的文件
        /// </summary>
        public async Task<(bool Success, string FilePath, string ErrorMessage)> SaveUploadedFileAsync(IFormFile file, string? customFileName = null)
        {
            try
            {
                var validation = ValidateFile(file);
                if (!validation.IsValid)
                {
                    return (false, string.Empty, validation.ErrorMessage);
                }

                // 生成唯一文件名
                var fileName = customFileName ?? GenerateUniqueFileName(file.FileName);
                var filePath = Path.Combine(_uploadPath, fileName);

                // 确保文件名唯一
                var counter = 1;
                var originalFilePath = filePath;
                while (File.Exists(filePath))
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFilePath);
                    var extension = Path.GetExtension(originalFilePath);
                    fileName = $"{nameWithoutExt}_{counter}{extension}";
                    filePath = Path.Combine(_uploadPath, fileName);
                    counter++;
                }

                // 保存文件
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                _logger.LogInformation("文件保存成功: {FilePath}", filePath);
                return (true, filePath, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存文件失败: {FileName}", file.FileName);
                return (false, string.Empty, $"保存文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成唯一文件名
        /// </summary>
        private string GenerateUniqueFileName(string originalFileName)
        {
            var extension = Path.GetExtension(originalFileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var guid = Guid.NewGuid().ToString("N")[..8];
            
            return $"{nameWithoutExt}_{timestamp}_{guid}{extension}";
        }

        /// <summary>
        /// 生成输出文件路径
        /// </summary>
        public string GenerateOutputFilePath(string originalFileName, string outputFormat, string? customName = null)
        {
            var nameWithoutExt = customName ?? Path.GetFileNameWithoutExtension(originalFileName);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{nameWithoutExt}_converted_{timestamp}.{outputFormat}";
            
            return Path.Combine(_outputPath, fileName);
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        public Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("文件删除成功: {FilePath}", filePath);
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除文件失败: {FilePath}", filePath);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 获取文件信息
        /// </summary>
        public FileInfo? GetFileInfo(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    return new FileInfo(filePath);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取文件信息失败: {FilePath}", filePath);
                return null;
            }
        }

        /// <summary>
        /// 清理旧文件
        /// </summary>
        public async Task<int> CleanupOldFilesAsync(int daysOld = 7)
        {
            var cleanedCount = 0;
            
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysOld);
                
                // 清理上传目录
                cleanedCount += await CleanupDirectoryAsync(_uploadPath, cutoffDate);
                
                // 清理输出目录
                cleanedCount += await CleanupDirectoryAsync(_outputPath, cutoffDate);
                
                _logger.LogInformation("清理了 {Count} 个旧文件", cleanedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理旧文件失败");
            }
            
            return cleanedCount;
        }

        /// <summary>
        /// 清理指定目录的旧文件
        /// </summary>
        private async Task<int> CleanupDirectoryAsync(string directoryPath, DateTime cutoffDate)
        {
            var cleanedCount = 0;
            
            if (!Directory.Exists(directoryPath))
            {
                return cleanedCount;
            }

            try
            {
                var files = Directory.GetFiles(directoryPath);
                
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        await Task.Run(() => File.Delete(file));
                        cleanedCount++;
                        _logger.LogDebug("删除旧文件: {FilePath}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理目录失败: {DirectoryPath}", directoryPath);
            }
            
            return cleanedCount;
        }

        /// <summary>
        /// 获取文件下载流
        /// </summary>
        public Task<(Stream? Stream, string ContentType, string FileName)> GetFileDownloadStreamAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return Task.FromResult<(Stream?, string, string)>((null, string.Empty, string.Empty));
                }

                var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var fileName = Path.GetFileName(filePath);
                var contentType = GetContentType(filePath);

                return Task.FromResult<(Stream?, string, string)>((stream, contentType, fileName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取文件下载流失败: {FilePath}", filePath);
                return Task.FromResult<(Stream?, string, string)>((null, string.Empty, string.Empty));
            }
        }

        /// <summary>
        /// 获取文件的Content-Type
        /// </summary>
        private string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            return extension switch
            {
                ".mp4" => "video/mp4",
                ".avi" => "video/x-msvideo",
                ".mov" => "video/quicktime",
                ".mkv" => "video/x-matroska",
                ".wmv" => "video/x-ms-wmv",
                ".flv" => "video/x-flv",
                ".webm" => "video/webm",
                ".m4v" => "video/x-m4v",
                ".3gp" => "video/3gpp",
                ".mp3" => "audio/mpeg",
                ".aac" => "audio/aac",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 获取支持的文件扩展名
        /// </summary>
        public string[] GetSupportedExtensions()
        {
            return _allowedExtensions;
        }

        /// <summary>
        /// 获取最大文件大小
        /// </summary>
        public long GetMaxFileSize()
        {
            return _maxFileSize;
        }
    }
}
