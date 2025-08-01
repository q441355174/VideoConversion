using System;

namespace VideoConversion_ClientTo.Application.DTOs
{
    /// <summary>
    /// 上传进度DTO
    /// 职责: 传输文件上传进度数据
    /// </summary>
    public class UploadProgressDto
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// 已上传字节数
        /// </summary>
        public long BytesUploaded { get; set; }

        /// <summary>
        /// 总字节数
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// 上传进度百分比 (0-100)
        /// </summary>
        public int Progress => TotalBytes > 0 ? (int)((double)BytesUploaded / TotalBytes * 100) : 0;

        /// <summary>
        /// 上传速度 (字节/秒)
        /// </summary>
        public long Speed { get; set; }

        /// <summary>
        /// 预计剩余时间（秒）
        /// </summary>
        public double? EstimatedRemainingSeconds { get; set; }

        /// <summary>
        /// 上传状态
        /// </summary>
        public string Status { get; set; } = "上传中";

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 格式化的进度文本
        /// </summary>
        public string FormattedProgress => $"{Progress}%";

        /// <summary>
        /// 格式化的速度文本
        /// </summary>
        public string FormattedSpeed => FormatSpeed(Speed);

        /// <summary>
        /// 格式化的文件大小
        /// </summary>
        public string FormattedSize => $"{FormatFileSize(BytesUploaded)} / {FormatFileSize(TotalBytes)}";

        /// <summary>
        /// 是否上传完成
        /// </summary>
        public bool IsCompleted => Progress >= 100;

        private string FormatSpeed(long bytesPerSecond)
        {
            if (bytesPerSecond == 0) return "0 B/s";

            string[] sizes = { "B/s", "KB/s", "MB/s", "GB/s" };
            int order = 0;
            double len = bytesPerSecond;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
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

        public override string ToString()
        {
            return $"上传进度: {FileName} - {FormattedProgress} ({FormattedSpeed})";
        }
    }
}
