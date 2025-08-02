using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Presentation.ViewModels;
using VideoConversion_ClientTo.Infrastructure;

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
        private readonly FFmpegService _ffmpegService;
        private readonly ThumbnailService _thumbnailService;
        private readonly string[] _supportedExtensions;

        public FilePreprocessorService(IFileDialogService fileDialogService)
        {
            _fileDialogService = fileDialogService;
            _ffmpegService = FFmpegService.Instance;
            _thumbnailService = ThumbnailService.Instance;
            _supportedExtensions = _fileDialogService.GetSupportedVideoExtensions();

            Utils.Logger.Info("FilePreprocessorService", "✅ 文件预处理服务已初始化，集成FFmpeg和缩略图服务");
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
                // 开始文件预处理（移除日志）
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

                    // 创建FileItemViewModel并进行视频分析
                    processedFile.ViewModel = await CreateFileItemViewModelWithAnalysisAsync(processedFile, progress);

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

        /// <summary>
        /// 创建FileItemViewModel并进行完整的视频分析
        /// </summary>
        private async Task<FileItemViewModel> CreateFileItemViewModelWithAnalysisAsync(
            ProcessedFileInfo fileInfo,
            IProgress<string>? progress = null)
        {
            var fileItem = new Presentation.ViewModels.FileItemViewModel
            {
                FileName = fileInfo.FileName,
                FilePath = fileInfo.FilePath,
                FileSize = FormatFileSize(fileInfo.FileSize),
                Status = Presentation.ViewModels.FileItemStatus.Pending,
                Progress = 0,
                IsConverting = false,
                CanConvert = true,
                LocalTaskId = Guid.NewGuid().ToString(),
                StatusText = "分析中..."
            };

            try
            {
                // 通知开始分析
                progress?.Report($"分析视频信息: {fileInfo.FileName}");

                // 使用FFmpeg获取视频信息
                var videoInfo = await _ffmpegService.GetVideoInfoAsync(fileInfo.FilePath);
                if (videoInfo != null)
                {
                    fileItem.SourceFormat = videoInfo.Format;
                    fileItem.SourceResolution = videoInfo.Resolution;
                    fileItem.Duration = videoInfo.Duration;
                    fileItem.EstimatedFileSize = videoInfo.EstimatedSize;
                    fileItem.EstimatedDuration = videoInfo.EstimatedDuration;

                    // 视频信息分析完成（移除日志）
                }

                // 生成缩略图
                progress?.Report($"生成缩略图: {fileInfo.FileName}");
                var thumbnail = await _thumbnailService.GetThumbnailAsync(fileInfo.FilePath, 100, 70);
                if (thumbnail != null)
                {
                    fileItem.Thumbnail = thumbnail;
                    Utils.Logger.Info("FilePreprocessorService", $"✅ 缩略图生成完成: {fileInfo.FileName}");
                }

                fileItem.StatusText = "等待处理";
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FilePreprocessorService", $"❌ 视频分析失败: {fileInfo.FileName} - {ex.Message}");

                // 设置默认值
                fileItem.SourceFormat = Path.GetExtension(fileInfo.FilePath).TrimStart('.').ToUpper();
                fileItem.SourceResolution = "未知";
                fileItem.Duration = "未知";
                fileItem.StatusText = "分析失败";
            }

            return fileItem;
        }

        private FileItemViewModel CreateFileItemViewModel(ProcessedFileInfo fileInfo)
        {
            return new Presentation.ViewModels.FileItemViewModel
            {
                FileName = fileInfo.FileName,
                FilePath = fileInfo.FilePath,
                FileSize = FormatFileSize(fileInfo.FileSize),
                Status = Presentation.ViewModels.FileItemStatus.Pending,
                Progress = 0,
                IsConverting = false,
                CanConvert = true
            };
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
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
