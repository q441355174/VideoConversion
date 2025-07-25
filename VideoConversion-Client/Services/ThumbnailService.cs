using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// 视频缩略图生成服务
    /// </summary>
    public class ThumbnailService
    {
        private static ThumbnailService? _instance;
        private static readonly object _lock = new object();
        private readonly string _thumbnailCacheDir;

        public static ThumbnailService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ThumbnailService();
                        }
                    }
                }
                return _instance;
            }
        }

        private ThumbnailService()
        {
            // 创建缩略图缓存目录
            _thumbnailCacheDir = Path.Combine(Path.GetTempPath(), "VideoConversion", "Thumbnails");
            Directory.CreateDirectory(_thumbnailCacheDir);
        }

        /// <summary>
        /// 获取视频缩略图
        /// </summary>
        /// <param name="videoPath">视频文件路径</param>
        /// <param name="width">缩略图宽度</param>
        /// <param name="height">缩略图高度</param>
        /// <returns>缩略图Bitmap，失败时返回null</returns>
        public async Task<Bitmap?> GetThumbnailAsync(string videoPath, int width = 100, int height = 70)
        {
            try
            {
                if (!File.Exists(videoPath))
                {
                    return null;
                }

                // 生成缓存文件名
                var videoFileInfo = new FileInfo(videoPath);
                var cacheFileName = $"{Path.GetFileNameWithoutExtension(videoPath)}_{videoFileInfo.LastWriteTime.Ticks}_{width}x{height}.jpg";
                var cachePath = Path.Combine(_thumbnailCacheDir, cacheFileName);

                // 检查缓存
                if (File.Exists(cachePath))
                {
                    try
                    {
                        return new Bitmap(cachePath);
                    }
                    catch
                    {
                        // 缓存文件损坏，删除并重新生成
                        File.Delete(cachePath);
                    }
                }

                // 生成新的缩略图
                var thumbnailPath = await GenerateThumbnailAsync(videoPath, cachePath, width, height);
                if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
                {
                    return new Bitmap(thumbnailPath);
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取缩略图失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 使用FFmpeg生成缩略图
        /// </summary>
        private async Task<string?> GenerateThumbnailAsync(string videoPath, string outputPath, int width, int height)
        {
            try
            {
                var ffmpegPath = FindFFmpegPath();
                if (string.IsNullOrEmpty(ffmpegPath))
                {
                    System.Diagnostics.Debug.WriteLine("未找到FFmpeg，无法生成缩略图");
                    return null;
                }

                // FFmpeg命令：在视频10%位置截取一帧作为缩略图
                var arguments = $"-i \"{videoPath}\" -ss 00:00:01 -vframes 1 -vf \"scale={width}:{height}\" -y \"{outputPath}\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null) return null;

                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && File.Exists(outputPath))
                {
                    return outputPath;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FFmpeg生成缩略图失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 查找FFmpeg路径
        /// </summary>
        private string? FindFFmpegPath()
        {
            var possiblePaths = new[]
            {
                "ffmpeg.exe",
                "ffmpeg",
                @"C:\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe")
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
                        var ffmpegPath = Path.Combine(path, "ffmpeg.exe");
                        if (File.Exists(ffmpegPath))
                        {
                            return ffmpegPath;
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

        /// <summary>
        /// 清理缩略图缓存
        /// </summary>
        public void ClearCache()
        {
            try
            {
                if (Directory.Exists(_thumbnailCacheDir))
                {
                    var files = Directory.GetFiles(_thumbnailCacheDir);
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // 忽略删除失败的文件
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理缩略图缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理过期的缓存文件
        /// </summary>
        public void ClearExpiredCache(TimeSpan maxAge)
        {
            try
            {
                if (!Directory.Exists(_thumbnailCacheDir)) return;

                var cutoffTime = DateTime.Now - maxAge;
                var files = Directory.GetFiles(_thumbnailCacheDir);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffTime)
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                        // 忽略删除失败的文件
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理过期缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取缓存目录大小
        /// </summary>
        public long GetCacheSize()
        {
            try
            {
                if (!Directory.Exists(_thumbnailCacheDir)) return 0;

                long totalSize = 0;
                var files = Directory.GetFiles(_thumbnailCacheDir);
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        totalSize += fileInfo.Length;
                    }
                    catch
                    {
                        // 忽略无法访问的文件
                    }
                }
                return totalSize;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 创建默认缩略图（用于无法生成缩略图的情况）
        /// </summary>
        public Bitmap? CreateDefaultThumbnail(int width = 100, int height = 70)
        {
            try
            {
                // 这里可以创建一个简单的默认图像
                // 暂时返回null，让UI显示占位符
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
