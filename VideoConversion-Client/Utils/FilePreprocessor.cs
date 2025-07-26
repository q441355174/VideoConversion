using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VideoConversion_Client.Models;
using VideoConversion_Client.Services;

namespace VideoConversion_Client.Utils
{
    /// <summary>
    /// 文件预处理工具类 - 处理拖动文件和计算转码数据
    /// </summary>
    public static class FilePreprocessor
    {
        /// <summary>
        /// 支持的视频文件扩展名
        /// </summary>
        private static readonly HashSet<string> SupportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".3gp",
            ".mpg", ".mpeg", ".ts", ".mts", ".m2ts", ".vob", ".asf", ".rm", ".rmvb"
        };

        /// <summary>
        /// 大文件阈值 (100MB)
        /// </summary>
        private const long LargeFileThreshold = 100 * 1024 * 1024;

        /// <summary>
        /// FFmpeg工具路径
        /// </summary>
        private static readonly string FFmpegPath = GetFFmpegPath();
        private static readonly string FFprobePath = GetFFprobePath();

        /// <summary>
        /// 获取FFmpeg路径，优先查找项目根目录
        /// </summary>
        private static string GetFFmpegPath()
        {
            // 首先检查应用程序目录
            var appDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (File.Exists(appDirPath))
                return appDirPath;

            // 检查项目根目录（开发时）
            var projectRoot = GetProjectRootDirectory();
            if (!string.IsNullOrEmpty(projectRoot))
            {
                var projectPath = Path.Combine(projectRoot, "ffmpeg.exe");
                if (File.Exists(projectPath))
                    return projectPath;
            }

            return appDirPath; // 返回默认路径
        }

        /// <summary>
        /// 获取FFprobe路径，优先查找项目根目录
        /// </summary>
        private static string GetFFprobePath()
        {
            // 首先检查应用程序目录
            var appDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffprobe.exe");
            if (File.Exists(appDirPath))
                return appDirPath;

            // 检查项目根目录（开发时）
            var projectRoot = GetProjectRootDirectory();
            if (!string.IsNullOrEmpty(projectRoot))
            {
                var projectPath = Path.Combine(projectRoot, "ffprobe.exe");
                if (File.Exists(projectPath))
                    return projectPath;
            }

            return appDirPath; // 返回默认路径
        }

        /// <summary>
        /// 获取项目根目录
        /// </summary>
        private static string? GetProjectRootDirectory()
        {
            try
            {
                var currentDir = AppDomain.CurrentDomain.BaseDirectory;
                var directory = new DirectoryInfo(currentDir);

                // 向上查找包含.csproj文件的目录
                while (directory != null && directory.Parent != null)
                {
                    if (directory.GetFiles("*.csproj").Any())
                    {
                        return directory.FullName;
                    }
                    directory = directory.Parent;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 是否可以使用本地FFmpeg
        /// </summary>
        private static readonly bool CanUseLocalFFmpeg = File.Exists(FFmpegPath) && File.Exists(FFprobePath);

        /// <summary>
        /// 预处理结果
        /// </summary>
        public class PreprocessResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public List<ProcessedFileInfo> ProcessedFiles { get; set; } = new();
            public List<string> SkippedFiles { get; set; } = new();
            public PreprocessStatistics Statistics { get; set; } = new();
        }

        /// <summary>
        /// 处理后的文件信息
        /// </summary>
        public class ProcessedFileInfo
        {
            public string FilePath { get; set; } = "";
            public string FileName { get; set; } = "";
            public string SafeFileName { get; set; } = "";
            public long FileSize { get; set; }
            public string FormattedFileSize { get; set; } = "";
            public string FileExtension { get; set; } = "";
            public bool IsLargeFile { get; set; }
            public FileItemViewModel? ViewModel { get; set; }
            public EstimatedConversionData? EstimatedData { get; set; }
        }

        /// <summary>
        /// 预估转换数据
        /// </summary>
        public class EstimatedConversionData
        {
            public string EstimatedFileSize { get; set; } = "计算中...";
            public string EstimatedDuration { get; set; } = "计算中...";
            public string TargetFormat { get; set; } = "MP4";
            public string TargetResolution { get; set; } = "1920×1080";
            public string TargetCodec { get; set; } = "H.264";
            public long EstimatedSizeBytes { get; set; }
            public double CompressionRatio { get; set; }
        }

        /// <summary>
        /// 简化的视频文件信息（仅用于FilePreprocessor）
        /// </summary>
        public class VideoFileInfo
        {
            public string Duration { get; set; } = "";
            public string Resolution { get; set; } = "";
            public string VideoCodec { get; set; } = "";
        }

        /// <summary>
        /// 预处理统计信息
        /// </summary>
        public class PreprocessStatistics
        {
            public int TotalFiles { get; set; }
            public int ProcessedFiles { get; set; }
            public int SkippedFiles { get; set; }
            public long TotalSize { get; set; }
            public long EstimatedOutputSize { get; set; }
            public int LargeFiles { get; set; }
            public string FormattedTotalSize { get; set; } = "";
            public string FormattedEstimatedSize { get; set; } = "";
        }

        /// <summary>
        /// 进度回调委托
        /// </summary>
        public delegate void ProgressCallback(string filePath, string status, double progress = -1);

        /// <summary>
        /// 文件完成回调委托
        /// </summary>
        public delegate void FileCompletedCallback(string filePath, bool success);

        /// <summary>
        /// 预处理拖动的文件列表
        /// </summary>
        /// <param name="filePaths">文件路径列表</param>
        /// <param name="includeSubdirectories">是否包含子目录</param>
        /// <param name="progressCallback">进度回调</param>
        /// <param name="fileCompletedCallback">文件完成回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>预处理结果</returns>
        public static async Task<PreprocessResult> PreprocessFilesAsync(
            IEnumerable<string> filePaths,
            bool includeSubdirectories = false,
            ProgressCallback? progressCallback = null,
            FileCompletedCallback? fileCompletedCallback = null,
            CancellationToken cancellationToken = default)
        {
            var result = new PreprocessResult { Success = true };

            try
            {
                // 1. 展开文件路径（处理目录）
                var expandedFiles = ExpandFilePaths(filePaths, includeSubdirectories);
                
                // 2. 过滤支持的视频文件
                var videoFiles = FilterVideoFiles(expandedFiles, result.SkippedFiles);
                
                // 3. 验证文件可访问性
                var validFiles = ValidateFiles(videoFiles, result.SkippedFiles);
                
                // 4. 生成安全文件名
                var processedFiles = GenerateProcessedFileInfo(validFiles);
                
                // 5. 异步获取视频信息和预估数据
                await EnrichWithVideoInfoAsync(processedFiles, progressCallback, fileCompletedCallback, cancellationToken);
                
                // 6. 计算统计信息
                result.Statistics = CalculateStatistics(processedFiles, result.SkippedFiles.Count);
                result.ProcessedFiles = processedFiles;

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"文件预处理失败: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 展开文件路径（处理目录）
        /// </summary>
        private static List<string> ExpandFilePaths(IEnumerable<string> paths, bool includeSubdirectories)
        {
            var expandedFiles = new List<string>();

            foreach (var path in paths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        expandedFiles.Add(path);
                    }
                    else if (Directory.Exists(path))
                    {
                        var searchOption = includeSubdirectories 
                            ? SearchOption.AllDirectories 
                            : SearchOption.TopDirectoryOnly;

                        var files = Directory.GetFiles(path, "*.*", searchOption);
                        expandedFiles.AddRange(files);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"展开路径失败 {path}: {ex.Message}");
                }
            }

            return expandedFiles;
        }

        /// <summary>
        /// 过滤支持的视频文件
        /// </summary>
        private static List<string> FilterVideoFiles(List<string> files, List<string> skippedFiles)
        {
            var videoFiles = new List<string>();

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (SupportedVideoExtensions.Contains(extension))
                {
                    videoFiles.Add(file);
                }
                else
                {
                    skippedFiles.Add($"{Path.GetFileName(file)} (不支持的格式: {extension})");
                }
            }

            return videoFiles;
        }

        /// <summary>
        /// 验证文件可访问性
        /// </summary>
        private static List<string> ValidateFiles(List<string> files, List<string> skippedFiles)
        {
            var validFiles = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    // 检查文件是否存在
                    if (!File.Exists(file))
                    {
                        skippedFiles.Add($"{Path.GetFileName(file)} (文件不存在)");
                        continue;
                    }

                    // 检查文件是否可读
                    using var stream = File.OpenRead(file);
                    if (stream.Length == 0)
                    {
                        skippedFiles.Add($"{Path.GetFileName(file)} (文件为空)");
                        continue;
                    }

                    validFiles.Add(file);
                }
                catch (Exception ex)
                {
                    skippedFiles.Add($"{Path.GetFileName(file)} (访问失败: {ex.Message})");
                }
            }

            return validFiles;
        }

        /// <summary>
        /// 生成处理后的文件信息
        /// </summary>
        private static List<ProcessedFileInfo> GenerateProcessedFileInfo(List<string> files)
        {
            var processedFiles = new List<ProcessedFileInfo>();

            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    var fileName = fileInfo.Name;
                    var safeFileName = FileNameHelper.GetSafeFileName(fileName);

                    var processed = new ProcessedFileInfo
                    {
                        FilePath = file,
                        FileName = fileName,
                        SafeFileName = safeFileName,
                        FileSize = fileInfo.Length,
                        FormattedFileSize = FileSizeFormatter.FormatBytesAuto(fileInfo.Length),
                        FileExtension = fileInfo.Extension.TrimStart('.').ToUpper(),
                        IsLargeFile = fileInfo.Length > LargeFileThreshold
                    };

                    processedFiles.Add(processed);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"生成文件信息失败 {file}: {ex.Message}");
                }
            }

            return processedFiles;
        }

        /// <summary>
        /// 同步获取视频信息和预估数据
        /// </summary>
        private static async Task EnrichWithVideoInfoAsync(
            List<ProcessedFileInfo> processedFiles,
            ProgressCallback? progressCallback = null,
            FileCompletedCallback? fileCompletedCallback = null,
            CancellationToken cancellationToken = default)
        {
            var settingsService = ConversionSettingsService.Instance;

            // 设置并发数：CPU核心数，但不超过4个并发任务，避免过度占用资源
            var maxConcurrency = Math.Min(Environment.ProcessorCount, 4);
            Utils.Logger.Info("FilePreprocessor", $"使用并发处理，最大并发数: {maxConcurrency}");

            // 使用SemaphoreSlim控制并发数
            using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            // 创建并发任务列表
            var tasks = processedFiles.Select(async file =>
            {
                // 等待获取信号量
                await semaphore.WaitAsync(cancellationToken);

                try
                {
                    await ProcessSingleFileAsync(file, settingsService, progressCallback, fileCompletedCallback, cancellationToken);
                }
                finally
                {
                    // 释放信号量
                    semaphore.Release();
                }
            });

            // 等待所有任务完成
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 处理单个文件
        /// </summary>
        private static async Task ProcessSingleFileAsync(
            ProcessedFileInfo file,
            ConversionSettingsService settingsService,
            ProgressCallback? progressCallback,
            FileCompletedCallback? fileCompletedCallback,
            CancellationToken cancellationToken)
        {
            // 检查是否已取消
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                // 通知开始处理
                progressCallback?.Invoke(file.FilePath, "开始分析...");

                System.Diagnostics.Debug.WriteLine($"开始处理文件: {file.FileName}");

                VideoFileInfo videoInfo;
                Avalonia.Media.Imaging.Bitmap? thumbnail = null;

                if (CanUseLocalFFmpeg)
                {
                    // 通知正在获取视频信息
                    progressCallback?.Invoke(file.FilePath, "获取视频信息...");

                    // 使用本地FFmpeg获取信息（同步等待完成）
                    videoInfo = await GetVideoInfoWithFFprobeAsync(file.FilePath);

                    // 通知正在生成缩略图
                    progressCallback?.Invoke(file.FilePath, "生成缩略图...");
                    thumbnail = await GenerateThumbnailWithFFmpegAsync(file.FilePath);

                    System.Diagnostics.Debug.WriteLine($"使用FFmpeg获取视频信息完成: {file.FileName}");
                }
                else
                {
                    // 通知正在生成缩略图
                    progressCallback?.Invoke(file.FilePath, "生成缩略图...");

                    // 回退到ThumbnailService，视频信息设为默认值
                    var thumbnailService = ThumbnailService.Instance;

                    videoInfo = new VideoFileInfo
                    {
                        Duration = "未知",
                        Resolution = "未知",
                        VideoCodec = "unknown"
                    };
                    thumbnail = await thumbnailService.GetThumbnailAsync(file.FilePath, 100, 70);

                    Utils.Logger.Warning("FilePreprocessor", $"FFmpeg不可用，使用默认视频信息: {file.FileName}");
                }

                // 通知正在计算预估数据
                progressCallback?.Invoke(file.FilePath, "计算预估数据...");

                // 计算预估转换数据
                var estimatedData = CalculateEstimatedData(videoInfo, file.FileSize, settingsService);
                file.EstimatedData = estimatedData;

                // 创建ViewModel
                file.ViewModel = CreateFileItemViewModel(file, videoInfo, estimatedData);

                // 设置缩略图
                if (file.ViewModel != null && thumbnail != null)
                {
                    file.ViewModel.Thumbnail = thumbnail;
                }

                // 调试输出
                System.Diagnostics.Debug.WriteLine($"文件处理完成: {file.FileName}, ViewModel创建成功: {file.ViewModel != null}");

                if (file.ViewModel != null)
                {
                    System.Diagnostics.Debug.WriteLine($"ViewModel详情 - 文件名: {file.ViewModel.FileName}, 分辨率: {file.ViewModel.SourceResolution}, 时长: {file.ViewModel.Duration}");
                }

                // 通知文件处理完成
                fileCompletedCallback?.Invoke(file.FilePath, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理文件失败 {file.FilePath}: {ex.Message}");

                // 创建默认的预估数据和ViewModel
                file.EstimatedData = CreateDefaultEstimatedData(settingsService);
                file.ViewModel = CreateDefaultFileItemViewModel(file);

                System.Diagnostics.Debug.WriteLine($"为失败文件创建默认ViewModel: {file.FileName}");

                // 通知文件处理失败
                fileCompletedCallback?.Invoke(file.FilePath, false);
            }
        }

        /// <summary>
        /// 使用FFprobe获取视频信息
        /// </summary>
        private static async Task<VideoFileInfo> GetVideoInfoWithFFprobeAsync(string filePath)
        {
            try
            {
                Utils.Logger.Info("FilePreprocessor", $"开始使用FFprobe分析文件: {Path.GetFileName(filePath)}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = FFprobePath,
                    Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                Utils.Logger.Info("FilePreprocessor", $"FFprobe命令: {FFprobePath} {startInfo.Arguments}");

                using var process = Process.Start(startInfo);
                if (process == null)
                    throw new Exception("无法启动FFprobe进程");

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                Utils.Logger.Info("FilePreprocessor", $"FFprobe执行完成，退出代码: {process.ExitCode}");

                if (!string.IsNullOrEmpty(error))
                {
                    Utils.Logger.Warning("FilePreprocessor", $"FFprobe错误输出: {error}");
                }

                if (process.ExitCode != 0)
                    throw new Exception($"FFprobe执行失败: {error}");

                Utils.Logger.Info("FilePreprocessor", $"FFprobe输出长度: {output.Length} 字符");
                return ParseFFprobeOutput(output);
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FilePreprocessor", $"FFprobe获取视频信息失败: {ex.Message}", ex);

                // 返回默认值
                Utils.Logger.Warning("FilePreprocessor", "FFprobe失败，返回默认视频信息");
                return new VideoFileInfo
                {
                    Duration = "未知",
                    Resolution = "未知",
                    VideoCodec = "unknown"
                };
            }
        }

        /// <summary>
        /// 预处理FFprobe的JSON输出，修复转义问题
        /// </summary>
        private static string PreprocessFFprobeJson(string jsonOutput)
        {
            try
            {
                // 修复文件路径中的反斜杠转义问题
                // 将 \h, \F, \C 等无效转义序列替换为 \\h, \\F, \\C
                var processed = jsonOutput;

                // 查找所有 "filename": "..." 字段并修复其中的路径
                var filenamePattern = @"""filename"":\s*""([^""]+)""";
                processed = System.Text.RegularExpressions.Regex.Replace(processed, filenamePattern, match =>
                {
                    var fullMatch = match.Value;
                    var filename = match.Groups[1].Value;

                    // 修复路径中的反斜杠：将单个反斜杠替换为双反斜杠（除了已经正确转义的）
                    var fixedFilename = filename
                        .Replace("\\\\", "\x00DOUBLE_BACKSLASH\x00") // 临时标记已经正确的双反斜杠
                        .Replace("\\", "\\\\") // 将单反斜杠替换为双反斜杠
                        .Replace("\x00DOUBLE_BACKSLASH\x00", "\\\\"); // 恢复原本正确的双反斜杠

                    return $@"""filename"": ""{fixedFilename}""";
                });

                return processed;
            }
            catch (Exception ex)
            {
                Utils.Logger.Warning("FilePreprocessor", $"预处理FFprobe JSON失败: {ex.Message}");
                return jsonOutput; // 返回原始输出
            }
        }

        /// <summary>
        /// 解析FFprobe输出
        /// </summary>
        private static VideoFileInfo ParseFFprobeOutput(string jsonOutput)
        {
            try
            {
                // 预处理JSON以修复转义问题
                var processedJson = PreprocessFFprobeJson(jsonOutput);

                using var document = JsonDocument.Parse(processedJson);
                var root = document.RootElement;

                var format = root.GetProperty("format");
                var streams = root.GetProperty("streams");

                // 查找视频流
                JsonElement? videoStream = null;
                foreach (var stream in streams.EnumerateArray())
                {
                    if (stream.GetProperty("codec_type").GetString() == "video")
                    {
                        videoStream = stream;
                        break;
                    }
                }

                if (!videoStream.HasValue)
                    throw new Exception("未找到视频流");

                var video = videoStream.Value;

                // 获取基本信息
                var duration = format.TryGetProperty("duration", out var durationProp)
                    ? double.Parse(durationProp.GetString() ?? "0")
                    : 0;

                var width = video.TryGetProperty("width", out var widthProp)
                    ? widthProp.GetInt32()
                    : 0;

                var height = video.TryGetProperty("height", out var heightProp)
                    ? heightProp.GetInt32()
                    : 0;

                var codecName = video.TryGetProperty("codec_name", out var codecProp)
                    ? codecProp.GetString() ?? "unknown"
                    : "unknown";

                // 格式化时长
                var formattedDuration = FormatDuration(duration);
                var resolution = $"{width}×{height}";

                return new VideoFileInfo
                {
                    Duration = formattedDuration,
                    Resolution = resolution,
                    VideoCodec = codecName
                };
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FilePreprocessor", $"解析FFprobe输出失败: {ex.Message}", ex);
                Utils.Logger.Error("FilePreprocessor", $"FFprobe原始输出: {jsonOutput}");

                // 如果预处理后的JSON不同，也记录处理后的版本
                var processedJson = PreprocessFFprobeJson(jsonOutput);
                if (processedJson != jsonOutput)
                {
                    Utils.Logger.Error("FilePreprocessor", $"FFprobe处理后输出: {processedJson}");
                }

                // 返回默认值
                return new VideoFileInfo
                {
                    Duration = "未知",
                    Resolution = "未知",
                    VideoCodec = "unknown"
                };
            }
        }

        /// <summary>
        /// 格式化时长（秒转换为时:分:秒）
        /// </summary>
        private static string FormatDuration(double seconds)
        {
            if (seconds <= 0) return "00:00";

            var timeSpan = TimeSpan.FromSeconds(seconds);

            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
            else
            {
                return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
        }

        /// <summary>
        /// 使用FFmpeg生成缩略图
        /// </summary>
        private static async Task<Avalonia.Media.Imaging.Bitmap?> GenerateThumbnailWithFFmpegAsync(string filePath)
        {
            try
            {
                // 创建临时文件用于保存缩略图
                var tempThumbnailPath = Path.Combine(Path.GetTempPath(), $"thumb_{Guid.NewGuid()}.jpg");

                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    Arguments = $"-i \"{filePath}\" -ss 00:00:01 -vframes 1 -vf \"scale=100:70:force_original_aspect_ratio=decrease,pad=100:70:(ow-iw)/2:(oh-ih)/2\" -y \"{tempThumbnailPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    throw new Exception("无法启动FFmpeg进程");

                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"FFmpeg生成缩略图失败: {error}");
                    throw new Exception($"FFmpeg执行失败: {error}");
                }

                // 检查文件是否生成成功
                if (!File.Exists(tempThumbnailPath))
                    throw new Exception("缩略图文件未生成");

                // 加载为Bitmap
                using var fileStream = File.OpenRead(tempThumbnailPath);
                var bitmap = new Avalonia.Media.Imaging.Bitmap(fileStream);

                // 清理临时文件
                try
                {
                    File.Delete(tempThumbnailPath);
                }
                catch
                {
                    // 忽略删除失败
                }

                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FFmpeg生成缩略图失败: {ex.Message}");

                // 回退到ThumbnailService
                try
                {
                    var thumbnailService = ThumbnailService.Instance;
                    return await thumbnailService.GetThumbnailAsync(filePath, 100, 70);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 计算预估转换数据
        /// </summary>
        private static EstimatedConversionData CalculateEstimatedData(
            VideoFileInfo videoInfo, 
            long originalSize, 
            ConversionSettingsService settingsService)
        {
            try
            {
                var duration = ParseDuration(videoInfo.Duration);
                var estimatedSizeBytes = settingsService.EstimateConvertedFileSize(duration, originalSize);
                var compressionRatio = originalSize > 0 ? (double)estimatedSizeBytes / originalSize : 1.0;

                return new EstimatedConversionData
                {
                    EstimatedFileSize = FileSizeFormatter.FormatBytesAuto(estimatedSizeBytes),
                    EstimatedDuration = videoInfo.Duration,
                    TargetFormat = "MP4",
                    TargetResolution = settingsService.GetFormattedResolution(),
                    TargetCodec = settingsService.CurrentSettings.VideoCodec,
                    EstimatedSizeBytes = estimatedSizeBytes,
                    CompressionRatio = compressionRatio
                };
            }
            catch
            {
                return CreateDefaultEstimatedData(settingsService);
            }
        }

        /// <summary>
        /// 创建默认预估数据
        /// </summary>
        private static EstimatedConversionData CreateDefaultEstimatedData(ConversionSettingsService settingsService)
        {
            return new EstimatedConversionData
            {
                EstimatedFileSize = "预估中...",
                EstimatedDuration = "预估中...",
                TargetFormat = "MP4",
                TargetResolution = settingsService.GetFormattedResolution(),
                TargetCodec = settingsService.CurrentSettings.VideoCodec,
                EstimatedSizeBytes = 0,
                CompressionRatio = 1.0
            };
        }

        /// <summary>
        /// 创建FileItemViewModel
        /// </summary>
        private static FileItemViewModel CreateFileItemViewModel(
            ProcessedFileInfo fileInfo, 
            VideoFileInfo videoInfo, 
            EstimatedConversionData estimatedData)
        {
            return new FileItemViewModel
            {
                FileName = fileInfo.FileName,
                FilePath = fileInfo.FilePath,
                SourceFormat = fileInfo.FileExtension,
                SourceResolution = videoInfo.Resolution,
                FileSize = fileInfo.FormattedFileSize,
                Duration = videoInfo.Duration,
                TargetFormat = estimatedData.TargetFormat,
                TargetResolution = estimatedData.TargetResolution,
                EstimatedFileSize = estimatedData.EstimatedFileSize,
                EstimatedDuration = estimatedData.EstimatedDuration,
                Status = FileItemStatus.Pending,
                Progress = 0,
                StatusText = "等待处理"
            };
        }

        /// <summary>
        /// 创建默认FileItemViewModel
        /// </summary>
        private static FileItemViewModel CreateDefaultFileItemViewModel(ProcessedFileInfo fileInfo)
        {
            var settingsService = ConversionSettingsService.Instance;
            
            return new FileItemViewModel
            {
                FileName = fileInfo.FileName,
                FilePath = fileInfo.FilePath,
                SourceFormat = fileInfo.FileExtension,
                SourceResolution = "分析中...",
                FileSize = fileInfo.FormattedFileSize,
                Duration = "分析中...",
                TargetFormat = "MP4",
                TargetResolution = settingsService.GetFormattedResolution(),
                EstimatedFileSize = "预估中...",
                EstimatedDuration = "预估中...",
                Status = FileItemStatus.Pending,
                Progress = 0,
                StatusText = "等待处理"
            };
        }

        /// <summary>
        /// 计算统计信息
        /// </summary>
        private static PreprocessStatistics CalculateStatistics(
            List<ProcessedFileInfo> processedFiles, 
            int skippedCount)
        {
            var totalSize = processedFiles.Sum(f => f.FileSize);
            var estimatedSize = processedFiles.Sum(f => f.EstimatedData?.EstimatedSizeBytes ?? 0);
            var largeFiles = processedFiles.Count(f => f.IsLargeFile);

            return new PreprocessStatistics
            {
                TotalFiles = processedFiles.Count + skippedCount,
                ProcessedFiles = processedFiles.Count,
                SkippedFiles = skippedCount,
                TotalSize = totalSize,
                EstimatedOutputSize = estimatedSize,
                LargeFiles = largeFiles,
                FormattedTotalSize = FileSizeFormatter.FormatBytesAuto(totalSize),
                FormattedEstimatedSize = FileSizeFormatter.FormatBytesAuto(estimatedSize)
            };
        }

        /// <summary>
        /// 解析时长字符串为秒数
        /// </summary>
        private static double ParseDuration(string durationStr)
        {
            try
            {
                if (string.IsNullOrEmpty(durationStr)) return 0;
                
                var parts = durationStr.Split(':');
                if (parts.Length == 2) // mm:ss
                {
                    if (int.TryParse(parts[0], out var minutes) && int.TryParse(parts[1], out var seconds))
                    {
                        return minutes * 60 + seconds;
                    }
                }
                else if (parts.Length == 3) // h:mm:ss
                {
                    if (int.TryParse(parts[0], out var hours) && 
                        int.TryParse(parts[1], out var minutes) && 
                        int.TryParse(parts[2], out var seconds))
                    {
                        return hours * 3600 + minutes * 60 + seconds;
                    }
                }
                
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 检查是否为支持的视频文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否支持</returns>
        public static bool IsSupportedVideoFile(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return SupportedVideoExtensions.Contains(extension);
        }

        /// <summary>
        /// 获取支持的文件扩展名列表
        /// </summary>
        /// <returns>支持的扩展名</returns>
        public static IReadOnlySet<string> GetSupportedExtensions()
        {
            return SupportedVideoExtensions;
        }

        /// <summary>
        /// 批量更新预估数据（当转换设置变化时）
        /// </summary>
        /// <param name="processedFiles">已处理的文件列表</param>
        public static async Task UpdateEstimatedDataAsync(List<ProcessedFileInfo> processedFiles)
        {
            var settingsService = ConversionSettingsService.Instance;

            var tasks = processedFiles.Select(async file =>
            {
                try
                {
                    VideoFileInfo videoInfo;

                    if (CanUseLocalFFmpeg)
                    {
                        // 优先使用FFmpeg获取视频信息
                        videoInfo = await GetVideoInfoWithFFprobeAsync(file.FilePath);
                        System.Diagnostics.Debug.WriteLine($"使用FFmpeg更新预估数据: {file.FileName}");
                    }
                    else
                    {
                        // FFmpeg不可用，使用默认值
                        videoInfo = new VideoFileInfo
                        {
                            Duration = "未知",
                            Resolution = "未知",
                            VideoCodec = "unknown"
                        };
                        Utils.Logger.Warning("FilePreprocessor", $"FFmpeg不可用，使用默认视频信息更新预估数据: {file.FileName}");
                    }

                    var estimatedData = CalculateEstimatedData(videoInfo, file.FileSize, settingsService);

                    file.EstimatedData = estimatedData;

                    if (file.ViewModel != null)
                    {
                        file.ViewModel.EstimatedFileSize = estimatedData.EstimatedFileSize;
                        file.ViewModel.EstimatedDuration = estimatedData.EstimatedDuration;
                        file.ViewModel.TargetResolution = estimatedData.TargetResolution;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"更新预估数据失败 {file.FilePath}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 检查FFmpeg工具的可用性
        /// </summary>
        /// <returns>FFmpeg可用性信息</returns>
        public static (bool Available, string Message) CheckFFmpegAvailability()
        {
            try
            {
                var ffmpegExists = File.Exists(FFmpegPath);
                var ffprobeExists = File.Exists(FFprobePath);

                if (ffmpegExists && ffprobeExists)
                {
                    return (true, $"FFmpeg工具可用 - FFmpeg: {FFmpegPath}, FFprobe: {FFprobePath}");
                }
                else
                {
                    var missing = new List<string>();
                    if (!ffmpegExists) missing.Add("ffmpeg.exe");
                    if (!ffprobeExists) missing.Add("ffprobe.exe");

                    return (false, $"FFmpeg工具不完整，缺少: {string.Join(", ", missing)}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"检查FFmpeg可用性时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取FFmpeg版本信息
        /// </summary>
        /// <returns>版本信息</returns>
        public static async Task<string> GetFFmpegVersionAsync()
        {
            try
            {
                if (!CanUseLocalFFmpeg)
                    return "FFmpeg不可用";

                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    return "无法启动FFmpeg进程";

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                // 提取版本信息的第一行
                var lines = output.Split('\n');
                return lines.Length > 0 ? lines[0].Trim() : "未知版本";
            }
            catch (Exception ex)
            {
                return $"获取版本信息失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 初始化并显示FFmpeg状态
        /// </summary>
        public static async Task InitializeAsync()
        {
            // 显示路径信息
            Logger.Info("FilePreprocessor", $"应用程序基目录: {AppDomain.CurrentDomain.BaseDirectory}");
            Logger.Info("FilePreprocessor", $"FFmpeg路径: {FFmpegPath}");
            Logger.Info("FilePreprocessor", $"FFprobe路径: {FFprobePath}");
            Logger.Info("FilePreprocessor", $"FFmpeg文件存在: {File.Exists(FFmpegPath)}");
            Logger.Info("FilePreprocessor", $"FFprobe文件存在: {File.Exists(FFprobePath)}");

            var (available, message) = CheckFFmpegAvailability();
            Logger.Info("FilePreprocessor", $"初始化: {message}");

            if (available)
            {
                var version = await GetFFmpegVersionAsync();
                Logger.Info("FilePreprocessor", $"FFmpeg版本: {version}");
                Logger.Info("FilePreprocessor", "将优先使用本地FFmpeg工具获取视频信息和生成缩略图");
            }
            else
            {
                Logger.Warning("FilePreprocessor", "将使用NReco.VideoInfo和ThumbnailService作为备选方案");
            }
        }
    }
}
