using System;

namespace VideoConversion_ClientTo.Application.DTOs
{
    /// <summary>
    /// 清理结果 - 与服务器端完全一致
    /// </summary>
    public class CleanupResult
    {
        /// <summary>
        /// 总清理文件数
        /// </summary>
        public int TotalCleanedFiles { get; set; }

        /// <summary>
        /// 总清理大小（字节）
        /// </summary>
        public long TotalCleanedSize { get; set; }

        /// <summary>
        /// 临时文件清理数量
        /// </summary>
        public int TempFilesCleanedCount { get; set; }

        /// <summary>
        /// 临时文件清理大小
        /// </summary>
        public long TempFilesCleanedSize { get; set; }

        /// <summary>
        /// 原文件清理数量
        /// </summary>
        public int OriginalFilesCleanedCount { get; set; }

        /// <summary>
        /// 原文件清理大小
        /// </summary>
        public long OriginalFilesCleanedSize { get; set; }

        /// <summary>
        /// 下载文件清理数量
        /// </summary>
        public int DownloadedFilesCleanedCount { get; set; }

        /// <summary>
        /// 下载文件清理大小
        /// </summary>
        public long DownloadedFilesCleanedSize { get; set; }

        /// <summary>
        /// 日志文件清理数量
        /// </summary>
        public int LogFilesCleanedCount { get; set; }

        /// <summary>
        /// 日志文件清理大小
        /// </summary>
        public long LogFilesCleanedSize { get; set; }

        /// <summary>
        /// 孤儿文件清理数量
        /// </summary>
        public int OrphanFilesCleanedCount { get; set; }

        /// <summary>
        /// 孤儿文件清理大小
        /// </summary>
        public long OrphanFilesCleanedSize { get; set; }

        /// <summary>
        /// 失败任务文件清理数量
        /// </summary>
        public int FailedTaskFilesCleanedCount { get; set; }

        /// <summary>
        /// 失败任务文件清理大小
        /// </summary>
        public long FailedTaskFilesCleanedSize { get; set; }

        /// <summary>
        /// 清理开始时间
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 清理结束时间
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 清理耗时
        /// </summary>
        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

        /// <summary>
        /// 格式化清理大小
        /// </summary>
        public string FormattedTotalSize => FormatBytes(TotalCleanedSize);

        /// <summary>
        /// 格式化字节大小
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:F2} {sizes[order]}";
        }

        /// <summary>
        /// 合并清理结果
        /// </summary>
        public void Merge(CleanupResult other)
        {
            if (other == null) return;

            TotalCleanedFiles += other.TotalCleanedFiles;
            TotalCleanedSize += other.TotalCleanedSize;
            TempFilesCleanedCount += other.TempFilesCleanedCount;
            TempFilesCleanedSize += other.TempFilesCleanedSize;
            OriginalFilesCleanedCount += other.OriginalFilesCleanedCount;
            OriginalFilesCleanedSize += other.OriginalFilesCleanedSize;
            DownloadedFilesCleanedCount += other.DownloadedFilesCleanedCount;
            DownloadedFilesCleanedSize += other.DownloadedFilesCleanedSize;
            LogFilesCleanedCount += other.LogFilesCleanedCount;
            LogFilesCleanedSize += other.LogFilesCleanedSize;
            OrphanFilesCleanedCount += other.OrphanFilesCleanedCount;
            OrphanFilesCleanedSize += other.OrphanFilesCleanedSize;
            FailedTaskFilesCleanedCount += other.FailedTaskFilesCleanedCount;
            FailedTaskFilesCleanedSize += other.FailedTaskFilesCleanedSize;
        }

        /// <summary>
        /// 重写ToString方法
        /// </summary>
        public override string ToString()
        {
            return $"清理完成: {TotalCleanedFiles}个文件, {FormattedTotalSize}, 耗时{Duration.TotalSeconds:F1}秒";
        }
    }
}
