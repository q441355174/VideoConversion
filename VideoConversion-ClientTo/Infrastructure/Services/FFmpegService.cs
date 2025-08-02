using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// FFmpeg集成服务 - 基于Client项目的完整实现
    /// </summary>
    public class FFmpegService
    {
        private static FFmpegService? _instance;
        private static readonly object _lock = new object();

        private string? _ffmpegPath;
        private string? _ffprobePath;
        private bool _isInitialized = false;

        public static FFmpegService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new FFmpegService();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// FFmpeg是否可用
        /// </summary>
        public bool IsAvailable => _isInitialized && !string.IsNullOrEmpty(_ffmpegPath) && File.Exists(_ffmpegPath);

        /// <summary>
        /// FFprobe是否可用
        /// </summary>
        public bool IsFFprobeAvailable => _isInitialized && !string.IsNullOrEmpty(_ffprobePath) && File.Exists(_ffprobePath);

        private FFmpegService()
        {
            InitializeFFmpegPaths();
        }

        /// <summary>
        /// 初始化FFmpeg路径
        /// </summary>
        private void InitializeFFmpegPaths()
        {
            try
            {
                Utils.Logger.Info("FFmpegService", "🔍 开始查找FFmpeg...");

                // 查找FFmpeg路径
                _ffmpegPath = FindFFmpegExecutable("ffmpeg.exe");
                _ffprobePath = FindFFmpegExecutable("ffprobe.exe");

                if (!string.IsNullOrEmpty(_ffmpegPath) && !string.IsNullOrEmpty(_ffprobePath))
                {
                    _isInitialized = true;
                    Utils.Logger.Info("FFmpegService", $"✅ FFmpeg已找到: {_ffmpegPath}");
                    Utils.Logger.Info("FFmpegService", $"✅ FFprobe已找到: {_ffprobePath}");
                }
                else
                {
                    Utils.Logger.Warning("FFmpegService", "⚠️ 未找到FFmpeg，将使用简化模式");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FFmpegService", $"❌ 初始化FFmpeg失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 查找FFmpeg可执行文件
        /// </summary>
        private string? FindFFmpegExecutable(string executableName)
        {
            try
            {
                // 1. 检查应用程序目录
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var localPath = Path.Combine(appDir, "ffmpeg", executableName);
                if (File.Exists(localPath))
                {
                    return localPath;
                }

                // 2. 检查ffmpeg子目录
                localPath = Path.Combine(appDir, executableName);
                if (File.Exists(localPath))
                {
                    return localPath;
                }

                // 3. 检查系统PATH
                var pathVariable = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathVariable))
                {
                    var paths = pathVariable.Split(Path.PathSeparator);
                    foreach (var path in paths)
                    {
                        try
                        {
                            var fullPath = Path.Combine(path, executableName);
                            if (File.Exists(fullPath))
                            {
                                return fullPath;
                            }
                        }
                        catch
                        {
                            // 忽略无效路径
                        }
                    }
                }

                // 4. 检查常见安装位置
                var commonPaths = new[]
                {
                    @"C:\ffmpeg\bin\" + executableName,
                    @"C:\Program Files\ffmpeg\bin\" + executableName,
                    @"C:\Program Files (x86)\ffmpeg\bin\" + executableName,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ffmpeg", "bin", executableName)
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FFmpegService", $"❌ 查找{executableName}失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取视频信息
        /// </summary>
        public async Task<VideoFileInfo?> GetVideoInfoAsync(string filePath)
        {
            if (!IsFFprobeAvailable)
            {
                Utils.Logger.Warning("FFmpegService", "FFprobe不可用，返回默认视频信息");
                return CreateDefaultVideoInfo(filePath);
            }

            try
            {
                Utils.Logger.Info("FFmpegService", $"🔍 开始分析视频文件: {Path.GetFileName(filePath)}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffprobePath,
                    Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    throw new Exception("无法启动FFprobe进程");

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Utils.Logger.Error("FFmpegService", $"FFprobe执行失败，退出码: {process.ExitCode}, 错误: {error}");
                    return CreateDefaultVideoInfo(filePath);
                }

                return ParseFFprobeOutput(output, filePath);
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FFmpegService", $"❌ 获取视频信息失败: {ex.Message}");
                return CreateDefaultVideoInfo(filePath);
            }
        }

        /// <summary>
        /// 生成视频缩略图
        /// </summary>
        public async Task<string?> GenerateThumbnailAsync(string videoPath, int width = 100, int height = 70)
        {
            if (!IsAvailable)
            {
                Utils.Logger.Warning("FFmpegService", "FFmpeg不可用，无法生成缩略图");
                return null;
            }

            try
            {
                // 创建临时文件用于保存缩略图
                var tempThumbnailPath = Path.Combine(Path.GetTempPath(), $"thumb_{Guid.NewGuid()}.jpg");

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = $"-i \"{videoPath}\" -ss 00:00:01 -vframes 1 -vf \"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2\" -y \"{tempThumbnailPath}\"",
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

                if (process.ExitCode == 0 && File.Exists(tempThumbnailPath))
                {
                    Utils.Logger.Info("FFmpegService", $"✅ 缩略图生成成功: {Path.GetFileName(videoPath)}");
                    return tempThumbnailPath;
                }
                else
                {
                    Utils.Logger.Warning("FFmpegService", $"⚠️ FFmpeg生成缩略图失败: {error}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FFmpegService", $"❌ 生成缩略图失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析FFprobe输出
        /// </summary>
        private VideoFileInfo ParseFFprobeOutput(string jsonOutput, string filePath)
        {
            try
            {
                using var document = JsonDocument.Parse(jsonOutput);
                var root = document.RootElement;

                if (!root.TryGetProperty("streams", out var streams))
                {
                    return CreateDefaultVideoInfo(filePath);
                }

                // 查找视频流
                JsonElement? videoStream = null;
                foreach (var stream in streams.EnumerateArray())
                {
                    if (stream.TryGetProperty("codec_type", out var codecType) && 
                        codecType.GetString() == "video")
                    {
                        videoStream = stream;
                        break;
                    }
                }

                if (!videoStream.HasValue)
                {
                    return CreateDefaultVideoInfo(filePath);
                }

                var format = root.GetProperty("format");
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
                var format_name = format.TryGetProperty("format_name", out var formatProp)
                    ? formatProp.GetString() ?? "unknown"
                    : "unknown";

                return new VideoFileInfo
                {
                    Format = format_name,
                    Resolution = resolution,
                    Duration = formattedDuration,
                    EstimatedSize = "预估中...",
                    EstimatedDuration = formattedDuration,
                    ThumbnailPath = null
                };
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FFmpegService", $"❌ 解析FFprobe输出失败: {ex.Message}");
                return CreateDefaultVideoInfo(filePath);
            }
        }

        /// <summary>
        /// 创建默认视频信息
        /// </summary>
        private VideoFileInfo CreateDefaultVideoInfo(string filePath)
        {
            var extension = Path.GetExtension(filePath).TrimStart('.').ToUpper();
            return new VideoFileInfo
            {
                Format = extension,
                Resolution = "未知",
                Duration = "未知",
                EstimatedSize = "预估中...",
                EstimatedDuration = "预估中...",
                ThumbnailPath = null
            };
        }

        /// <summary>
        /// 格式化时长
        /// </summary>
        private string FormatDuration(double seconds)
        {
            if (seconds <= 0) return "00:00:00";

            var timeSpan = TimeSpan.FromSeconds(seconds);
            return timeSpan.ToString(@"hh\:mm\:ss");
        }
    }

    /// <summary>
    /// 视频文件信息
    /// </summary>
    public class VideoFileInfo
    {
        public string Format { get; set; } = "";
        public string Resolution { get; set; } = "";
        public string Duration { get; set; } = "";
        public string EstimatedSize { get; set; } = "";
        public string EstimatedDuration { get; set; } = "";
        public string? ThumbnailPath { get; set; }
    }
}
