using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// 视频信息获取服务
    /// </summary>
    public class VideoInfoService
    {
        private static VideoInfoService? _instance;
        private static readonly object _lock = new object();

        public static VideoInfoService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new VideoInfoService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 获取视频文件信息
        /// </summary>
        /// <param name="filePath">视频文件路径</param>
        /// <returns>视频信息</returns>
        public async Task<VideoFileInfo> GetVideoInfoAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return VideoFileInfo.CreateDefault(filePath);
                }

                var fileInfo = new FileInfo(filePath);
                var videoInfo = new VideoFileInfo
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    Format = Path.GetExtension(filePath).TrimStart('.').ToUpper()
                };

                // 尝试使用FFprobe获取视频信息
                var ffprobeInfo = await GetVideoInfoWithFFprobe(filePath);
                if (ffprobeInfo != null)
                {
                    videoInfo.Resolution = ffprobeInfo.Resolution;
                    videoInfo.Duration = ffprobeInfo.Duration;
                    videoInfo.FrameRate = ffprobeInfo.FrameRate;
                    videoInfo.VideoCodec = ffprobeInfo.VideoCodec;
                    videoInfo.AudioCodec = ffprobeInfo.AudioCodec;
                    videoInfo.BitRate = ffprobeInfo.BitRate;
                }
                else
                {
                    // 回退到默认值
                    videoInfo.Resolution = "未知";
                    videoInfo.Duration = "00:00:00";
                }

                return videoInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取视频信息失败: {ex.Message}");
                return VideoFileInfo.CreateDefault(filePath);
            }
        }

        /// <summary>
        /// 使用FFprobe获取视频信息
        /// </summary>
        private async Task<VideoFileInfo?> GetVideoInfoWithFFprobe(string filePath)
        {
            try
            {
                // 查找FFprobe路径
                var ffprobePath = FindFFprobePath();
                if (string.IsNullOrEmpty(ffprobePath))
                {
                    System.Diagnostics.Debug.WriteLine("未找到FFprobe，使用默认信息");
                    return null;
                }

                var arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"";
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null) return null;

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0) return null;

                return ParseFFprobeOutput(output, filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FFprobe执行失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析FFprobe输出
        /// </summary>
        private VideoFileInfo? ParseFFprobeOutput(string jsonOutput, string filePath)
        {
            try
            {
                using var document = JsonDocument.Parse(jsonOutput);
                var root = document.RootElement;

                var videoInfo = VideoFileInfo.CreateDefault(filePath);

                // 解析格式信息
                if (root.TryGetProperty("format", out var format))
                {
                    if (format.TryGetProperty("duration", out var duration))
                    {
                        if (double.TryParse(duration.GetString(), out var durationSeconds))
                        {
                            videoInfo.Duration = TimeSpan.FromSeconds(durationSeconds).ToString(@"hh\:mm\:ss");
                        }
                    }

                    if (format.TryGetProperty("bit_rate", out var bitRate))
                    {
                        if (long.TryParse(bitRate.GetString(), out var bitRateValue))
                        {
                            videoInfo.BitRate = bitRateValue;
                        }
                    }
                }

                // 解析流信息
                if (root.TryGetProperty("streams", out var streams))
                {
                    foreach (var stream in streams.EnumerateArray())
                    {
                        if (stream.TryGetProperty("codec_type", out var codecType))
                        {
                            var type = codecType.GetString();
                            
                            if (type == "video")
                            {
                                // 视频流信息
                                if (stream.TryGetProperty("width", out var width) && 
                                    stream.TryGetProperty("height", out var height))
                                {
                                    videoInfo.Resolution = $"{width.GetInt32()}×{height.GetInt32()}";
                                }

                                if (stream.TryGetProperty("codec_name", out var videoCodec))
                                {
                                    videoInfo.VideoCodec = videoCodec.GetString() ?? "";
                                }

                                if (stream.TryGetProperty("r_frame_rate", out var frameRate))
                                {
                                    var frameRateStr = frameRate.GetString();
                                    if (!string.IsNullOrEmpty(frameRateStr) && frameRateStr.Contains('/'))
                                    {
                                        var parts = frameRateStr.Split('/');
                                        if (parts.Length == 2 && 
                                            double.TryParse(parts[0], out var num) && 
                                            double.TryParse(parts[1], out var den) && 
                                            den != 0)
                                        {
                                            videoInfo.FrameRate = $"{num / den:F2} fps";
                                        }
                                    }
                                }
                            }
                            else if (type == "audio")
                            {
                                // 音频流信息
                                if (stream.TryGetProperty("codec_name", out var audioCodec))
                                {
                                    videoInfo.AudioCodec = audioCodec.GetString() ?? "";
                                }
                            }
                        }
                    }
                }

                return videoInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解析FFprobe输出失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 查找FFprobe路径
        /// </summary>
        private string? FindFFprobePath()
        {
            // 常见的FFprobe路径
            var possiblePaths = new[]
            {
                "ffprobe.exe",
                "ffprobe",
                @"C:\ffmpeg\bin\ffprobe.exe",
                @"C:\Program Files\ffmpeg\bin\ffprobe.exe",
                @"C:\Program Files (x86)\ffmpeg\bin\ffprobe.exe",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffprobe.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffprobe.exe")
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
                catch
                {
                    // 忽略权限错误等
                }
            }

            // 尝试从PATH环境变量中查找
            try
            {
                var pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    var paths = pathEnv.Split(Path.PathSeparator);
                    foreach (var path in paths)
                    {
                        var ffprobePath = Path.Combine(path, "ffprobe.exe");
                        if (File.Exists(ffprobePath))
                        {
                            return ffprobePath;
                        }
                    }
                }
            }
            catch
            {
                // 忽略错误
            }

            return null;
        }
    }

    /// <summary>
    /// 视频文件信息
    /// </summary>
    public class VideoFileInfo
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
        public string Format { get; set; } = "";
        public string Resolution { get; set; } = "";
        public string Duration { get; set; } = "";
        public string FrameRate { get; set; } = "";
        public string VideoCodec { get; set; } = "";
        public string AudioCodec { get; set; } = "";
        public long BitRate { get; set; }

        /// <summary>
        /// 创建默认视频信息
        /// </summary>
        public static VideoFileInfo CreateDefault(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            return new VideoFileInfo
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                Format = Path.GetExtension(filePath).TrimStart('.').ToUpper(),
                Resolution = "获取中...",
                Duration = "获取中...",
                FrameRate = "",
                VideoCodec = "",
                AudioCodec = "",
                BitRate = 0
            };
        }

        /// <summary>
        /// 获取格式化的文件大小
        /// </summary>
        public string GetFormattedSize()
        {
            return Utils.FileSizeFormatter.FormatBytesAuto(FileSize);
        }
    }
}
