using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Presentation.ViewModels;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// 文件预处理服务
    /// 职责: 处理文件验证、信息获取、缩略图生成等
    /// </summary>
    public interface IFilePreprocessorService
    {
        Task<PreprocessResult> PreprocessFilesAsync(
            IEnumerable<string> filePaths,
            bool includeSubdirectories = false,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default);
    }

    public class FilePreprocessorService : IFilePreprocessorService
    {
        private readonly IFileDialogService _fileDialogService;
        private readonly string[] _supportedExtensions;

        public FilePreprocessorService(IFileDialogService fileDialogService)
        {
            _fileDialogService = fileDialogService;
            _supportedExtensions = _fileDialogService.GetSupportedVideoExtensions();
        }

        public async Task<PreprocessResult> PreprocessFilesAsync(
            IEnumerable<string> filePaths,
            bool includeSubdirectories = false,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new PreprocessResult { Success = true };

            try
            {
                Utils.Logger.Info("FilePreprocessorService", "🔄 开始文件预处理");
                progress?.Report("开始文件预处理...");

                // 1. 展开文件路径（处理目录）
                var expandedFiles = ExpandFilePaths(filePaths, includeSubdirectories);
                progress?.Report($"发现 {expandedFiles.Count} 个文件");

                // 2. 过滤支持的视频文件
                var videoFiles = FilterVideoFiles(expandedFiles, result.SkippedFiles);
                progress?.Report($"筛选出 {videoFiles.Count} 个视频文件");

                // 3. 验证文件可访问性
                var validFiles = ValidateFiles(videoFiles, result.SkippedFiles);
                progress?.Report($"验证通过 {validFiles.Count} 个文件");

                // 4. 生成文件信息
                var processedFiles = await GenerateFileInfoAsync(validFiles, progress, cancellationToken);

                // 5. 计算统计信息
                result.Statistics = CalculateStatistics(processedFiles, result.SkippedFiles.Count);
                result.ProcessedFiles = processedFiles;

                Utils.Logger.Info("FilePreprocessorService", $"✅ 文件预处理完成: {processedFiles.Count} 个文件");
                progress?.Report("文件预处理完成");

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"文件预处理失败: {ex.Message}";
                Utils.Logger.Error("FilePreprocessorService", $"❌ 文件预处理失败: {ex.Message}");
                return result;
            }
        }

        private List<string> ExpandFilePaths(IEnumerable<string> filePaths, bool includeSubdirectories)
        {
            var expandedFiles = new List<string>();

            foreach (var path in filePaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        expandedFiles.Add(path);
                    }
                    else if (Directory.Exists(path))
                    {
                        var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                        var files = Directory.GetFiles(path, "*.*", searchOption);
                        expandedFiles.AddRange(files);
                    }
                }
                catch (Exception ex)
                {
                    Utils.Logger.Warning("FilePreprocessorService", $"⚠️ 展开路径失败 {path}: {ex.Message}");
                }
            }

            return expandedFiles;
        }

        private List<string> FilterVideoFiles(List<string> files, List<SkippedFileInfo> skippedFiles)
        {
            var videoFiles = new List<string>();

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (_supportedExtensions.Contains(extension))
                {
                    videoFiles.Add(file);
                }
                else
                {
                    skippedFiles.Add(new SkippedFileInfo
                    {
                        FilePath = file,
                        Reason = $"不支持的文件格式: {extension}"
                    });
                }
            }

            return videoFiles;
        }

        private List<string> ValidateFiles(List<string> files, List<SkippedFileInfo> skippedFiles)
        {
            var validFiles = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (!fileInfo.Exists)
                    {
                        skippedFiles.Add(new SkippedFileInfo
                        {
                            FilePath = file,
                            Reason = "文件不存在"
                        });
                        continue;
                    }

                    if (fileInfo.Length == 0)
                    {
                        skippedFiles.Add(new SkippedFileInfo
                        {
                            FilePath = file,
                            Reason = "文件大小为0"
                        });
                        continue;
                    }

                    // 检查文件是否被占用
                    try
                    {
                        using var stream = File.OpenRead(file);
                        validFiles.Add(file);
                    }
                    catch (IOException)
                    {
                        skippedFiles.Add(new SkippedFileInfo
                        {
                            FilePath = file,
                            Reason = "文件被占用或无法访问"
                        });
                    }
                }
                catch (Exception ex)
                {
                    skippedFiles.Add(new SkippedFileInfo
                    {
                        FilePath = file,
                        Reason = $"验证失败: {ex.Message}"
                    });
                }
            }

            return validFiles;
        }

        private async Task<List<ProcessedFileInfo>> GenerateFileInfoAsync(
            List<string> validFiles, 
            IProgress<string>? progress, 
            CancellationToken cancellationToken)
        {
            var processedFiles = new List<ProcessedFileInfo>();

            for (int i = 0; i < validFiles.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var file = validFiles[i];
                progress?.Report($"处理文件 {i + 1}/{validFiles.Count}: {Path.GetFileName(file)}");

                try
                {
                    var fileInfo = new FileInfo(file);
                    var processedFile = new ProcessedFileInfo
                    {
                        FilePath = file,
                        FileName = fileInfo.Name,
                        FileSize = fileInfo.Length,
                        FileExtension = fileInfo.Extension.ToLowerInvariant(),
                        FormattedFileSize = FormatFileSize(fileInfo.Length),
                        CreatedTime = fileInfo.CreationTime,
                        ModifiedTime = fileInfo.LastWriteTime
                    };

                    // 创建FileItemViewModel
                    processedFile.ViewModel = CreateFileItemViewModel(processedFile);

                    processedFiles.Add(processedFile);
                    Utils.Logger.Debug("FilePreprocessorService", $"📁 处理文件: {processedFile.FileName}");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Warning("FilePreprocessorService", $"⚠️ 处理文件失败 {file}: {ex.Message}");
                }
            }

            return processedFiles;
        }

        private FileItemViewModel CreateFileItemViewModel(ProcessedFileInfo fileInfo)
        {
            return new FileItemViewModel
            {
                FileName = fileInfo.FileName,
                FilePath = fileInfo.FilePath,
                FileSize = fileInfo.FileSize,
                Status = "等待处理",
                Progress = 0,
                IsConverting = false,
                IsCompleted = false
            };
        }

        private PreprocessStatistics CalculateStatistics(List<ProcessedFileInfo> processedFiles, int skippedCount)
        {
            return new PreprocessStatistics
            {
                TotalFiles = processedFiles.Count + skippedCount,
                ProcessedFiles = processedFiles.Count,
                SkippedFiles = skippedCount,
                TotalSize = processedFiles.Sum(f => f.FileSize),
                FormattedTotalSize = FormatFileSize(processedFiles.Sum(f => f.FileSize))
            };
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    #region 数据模型

    public class PreprocessResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<ProcessedFileInfo> ProcessedFiles { get; set; } = new();
        public List<SkippedFileInfo> SkippedFiles { get; set; } = new();
        public PreprocessStatistics? Statistics { get; set; }
    }

    public class ProcessedFileInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileExtension { get; set; } = string.Empty;
        public string FormattedFileSize { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public DateTime ModifiedTime { get; set; }
        public FileItemViewModel? ViewModel { get; set; }
    }

    public class SkippedFileInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class PreprocessStatistics
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public long TotalSize { get; set; }
        public string FormattedTotalSize { get; set; } = string.Empty;
    }

    #endregion
}
