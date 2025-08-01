using System;

namespace VideoConversion_ClientTo.Application.DTOs
{
    /// <summary>
    /// 空间检查响应DTO
    /// 职责: 传输空间检查结果数据
    /// </summary>
    public class SpaceCheckResponseDto
    {
        /// <summary>
        /// 是否有足够空间
        /// </summary>
        public bool HasEnoughSpace { get; set; }

        /// <summary>
        /// 请求的空间大小（字节）
        /// </summary>
        public long RequiredBytes { get; set; }

        /// <summary>
        /// 当前可用空间（字节）
        /// </summary>
        public long AvailableBytes { get; set; }

        /// <summary>
        /// 总空间（字节）
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// 已使用空间（字节）
        /// </summary>
        public long UsedBytes { get; set; }

        /// <summary>
        /// 空间不足时的缺少字节数
        /// </summary>
        public long ShortfallBytes => HasEnoughSpace ? 0 : RequiredBytes - AvailableBytes;

        /// <summary>
        /// 使用率百分比
        /// </summary>
        public double UsagePercentage => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;

        /// <summary>
        /// 检查消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 建议操作
        /// </summary>
        public string? Suggestion { get; set; }

        /// <summary>
        /// 检查时间
        /// </summary>
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 格式化的请求空间大小
        /// </summary>
        public string FormattedRequiredSize => FormatFileSize(RequiredBytes);

        /// <summary>
        /// 格式化的可用空间大小
        /// </summary>
        public string FormattedAvailableSize => FormatFileSize(AvailableBytes);

        /// <summary>
        /// 格式化的总空间大小
        /// </summary>
        public string FormattedTotalSize => FormatFileSize(TotalBytes);

        /// <summary>
        /// 格式化的已使用空间大小
        /// </summary>
        public string FormattedUsedSize => FormatFileSize(UsedBytes);

        /// <summary>
        /// 格式化的缺少空间大小
        /// </summary>
        public string FormattedShortfallSize => FormatFileSize(ShortfallBytes);

        /// <summary>
        /// 格式化的使用率
        /// </summary>
        public string FormattedUsagePercentage => $"{UsagePercentage:F1}%";

        /// <summary>
        /// 检查结果图标
        /// </summary>
        public string ResultIcon => HasEnoughSpace ? "✅" : "❌";

        /// <summary>
        /// 检查结果文本
        /// </summary>
        public string ResultText => HasEnoughSpace ? "空间充足" : "空间不足";

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

        /// <summary>
        /// 创建空间充足的响应
        /// </summary>
        public static SpaceCheckResponseDto CreateSufficient(long requiredBytes, long availableBytes, long totalBytes, long usedBytes)
        {
            return new SpaceCheckResponseDto
            {
                HasEnoughSpace = true,
                RequiredBytes = requiredBytes,
                AvailableBytes = availableBytes,
                TotalBytes = totalBytes,
                UsedBytes = usedBytes,
                Message = "磁盘空间充足，可以进行操作",
                CheckedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 创建空间不足的响应
        /// </summary>
        public static SpaceCheckResponseDto CreateInsufficient(long requiredBytes, long availableBytes, long totalBytes, long usedBytes)
        {
            var shortfall = requiredBytes - availableBytes;
            return new SpaceCheckResponseDto
            {
                HasEnoughSpace = false,
                RequiredBytes = requiredBytes,
                AvailableBytes = availableBytes,
                TotalBytes = totalBytes,
                UsedBytes = usedBytes,
                Message = $"磁盘空间不足，还需要 {new SpaceCheckResponseDto().FormatFileSize(shortfall)} 空间",
                Suggestion = "请清理磁盘空间或选择其他存储位置",
                CheckedAt = DateTime.UtcNow
            };
        }

        public override string ToString()
        {
            return $"空间检查: {ResultText} - 需要 {FormattedRequiredSize}，可用 {FormattedAvailableSize}";
        }
    }
}
