using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// 缩略图服务 - 基于Client项目的完整实现
    /// </summary>
    public class ThumbnailService
    {
        private static ThumbnailService? _instance;
        private static readonly object _lock = new object();

        private readonly string _thumbnailCacheDir;
        private readonly FFmpegService _ffmpegService;

        public static ThumbnailService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ThumbnailService();
                    }
                }
                return _instance;
            }
        }

        private ThumbnailService()
        {
            // 创建缓存目录
            _thumbnailCacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VideoConversion",
                "ThumbnailCache"
            );

            if (!Directory.Exists(_thumbnailCacheDir))
            {
                Directory.CreateDirectory(_thumbnailCacheDir);
            }

            _ffmpegService = FFmpegService.Instance;

            Utils.Logger.Info("ThumbnailService", $"✅ 缩略图服务已初始化，缓存目录: {_thumbnailCacheDir}");
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
                    Utils.Logger.Warning("ThumbnailService", $"视频文件不存在: {videoPath}");
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
                        Utils.Logger.Debug("ThumbnailService", $"📁 使用缓存缩略图: {Path.GetFileName(videoPath)}");
                        return new Bitmap(cachePath);
                    }
                    catch (Exception ex)
                    {
                        // 缓存文件损坏，删除并重新生成
                        Utils.Logger.Warning("ThumbnailService", $"⚠️ 缓存文件损坏，删除重新生成: {ex.Message}");
                        File.Delete(cachePath);
                    }
                }

                // 生成新的缩略图
                var thumbnailPath = await GenerateThumbnailAsync(videoPath, cachePath, width, height);
                if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
                {
                    Utils.Logger.Info("ThumbnailService", $"✅ 缩略图生成成功: {Path.GetFileName(videoPath)}");
                    return new Bitmap(thumbnailPath);
                }

                Utils.Logger.Warning("ThumbnailService", $"⚠️ 缩略图生成失败: {Path.GetFileName(videoPath)}");
                return null;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ThumbnailService", $"❌ 获取缩略图失败: {ex.Message}");
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
                Utils.Logger.Info("ThumbnailService", $"🎬 开始生成缩略图: {Path.GetFileName(videoPath)}");

                // 使用FFmpeg服务生成缩略图
                var tempThumbnailPath = await _ffmpegService.GenerateThumbnailAsync(videoPath, width, height);
                
                if (!string.IsNullOrEmpty(tempThumbnailPath) && File.Exists(tempThumbnailPath))
                {
                    // 将临时文件移动到缓存目录
                    File.Move(tempThumbnailPath, outputPath);
                    return outputPath;
                }

                // FFmpeg失败，尝试使用系统缩略图API（Windows）
                return await GenerateSystemThumbnailAsync(videoPath, outputPath, width, height);
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ThumbnailService", $"❌ 生成缩略图失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 使用系统API生成缩略图（Windows）
        /// </summary>
        private async Task<string?> GenerateSystemThumbnailAsync(string videoPath, string outputPath, int width, int height)
        {
            try
            {
                // 这里可以实现Windows Shell API的缩略图生成
                // 由于复杂性，暂时返回null，使用默认图标
                Utils.Logger.Info("ThumbnailService", "⚠️ 系统缩略图API暂未实现，返回null");
                return null;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ThumbnailService", $"❌ 系统缩略图生成失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public async Task CleanupCacheAsync(int maxAgeInDays = 30)
        {
            try
            {
                Utils.Logger.Info("ThumbnailService", $"🧹 开始清理缩略图缓存，保留{maxAgeInDays}天内的文件");

                if (!Directory.Exists(_thumbnailCacheDir))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-maxAgeInDays);
                var files = Directory.GetFiles(_thumbnailCacheDir);
                int deletedCount = 0;
                long freedSpace = 0;

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastAccessTime < cutoffDate)
                        {
                            freedSpace += fileInfo.Length;
                            File.Delete(file);
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Warning("ThumbnailService", $"⚠️ 删除缓存文件失败: {Path.GetFileName(file)} - {ex.Message}");
                    }
                }

                Utils.Logger.Info("ThumbnailService", $"✅ 缓存清理完成，删除{deletedCount}个文件，释放{FormatBytes(freedSpace)}空间");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ThumbnailService", $"❌ 清理缓存失败: {ex.Message}");
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
        /// 获取缓存文件数量
        /// </summary>
        public int GetCacheFileCount()
        {
            try
            {
                if (!Directory.Exists(_thumbnailCacheDir)) return 0;
                return Directory.GetFiles(_thumbnailCacheDir).Length;
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

        /// <summary>
        /// 格式化字节大小
        /// </summary>
        private string FormatBytes(long bytes)
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

        /// <summary>
        /// 预热缓存 - 为指定文件预生成缩略图
        /// </summary>
        public async Task PrewarmCacheAsync(string[] videoPaths, int width = 100, int height = 70)
        {
            try
            {
                Utils.Logger.Info("ThumbnailService", $"🔥 开始预热缩略图缓存，文件数: {videoPaths.Length}");

                var tasks = new List<Task>();
                foreach (var videoPath in videoPaths)
                {
                    tasks.Add(GetThumbnailAsync(videoPath, width, height));
                    
                    // 限制并发数量，避免系统负载过高
                    if (tasks.Count >= 5)
                    {
                        await Task.WhenAll(tasks);
                        tasks.Clear();
                    }
                }

                // 处理剩余任务
                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                }

                Utils.Logger.Info("ThumbnailService", "✅ 缩略图缓存预热完成");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ThumbnailService", $"❌ 预热缓存失败: {ex.Message}");
            }
        }
    }
}
