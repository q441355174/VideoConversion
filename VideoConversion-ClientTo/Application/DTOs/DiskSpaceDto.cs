using System;

namespace VideoConversion_ClientTo.Application.DTOs
{
    /// <summary>
    /// 磁盘空间信息DTO
    /// 职责: 传输磁盘空间相关数据
    /// </summary>
    public class DiskSpaceDto
    {
        /// <summary>
        /// 已使用空间（字节）
        /// </summary>
        public long UsedSpace { get; set; }

        /// <summary>
        /// 总空间（字节）
        /// </summary>
        public long TotalSpace { get; set; }

        /// <summary>
        /// 可用空间（字节）
        /// </summary>
        public long AvailableSpace { get; set; }

        /// <summary>
        /// 使用率百分比
        /// </summary>
        public double UsagePercentage => TotalSpace > 0 ? (double)UsedSpace / TotalSpace * 100 : 0;

        /// <summary>
        /// 格式化的已使用空间
        /// </summary>
        public string FormattedUsedSpace => FormatFileSize(UsedSpace);

        /// <summary>
        /// 格式化的总空间
        /// </summary>
        public string FormattedTotalSpace => FormatFileSize(TotalSpace);

        /// <summary>
        /// 格式化的可用空间
        /// </summary>
        public string FormattedAvailableSpace => FormatFileSize(AvailableSpace);

        /// <summary>
        /// 是否空间不足
        /// </summary>
        public bool IsLowSpace => UsagePercentage > 90;

        /// <summary>
        /// 是否空间警告
        /// </summary>
        public bool IsSpaceWarning => UsagePercentage > 80;

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

        /// <summary>
        /// 获取空间状态描述
        /// </summary>
        public string GetSpaceStatusDescription()
        {
            if (IsLowSpace)
                return "磁盘空间不足，请及时清理";
            if (IsSpaceWarning)
                return "磁盘空间使用率较高";
            return "磁盘空间充足";
        }

        /// <summary>
        /// 创建默认实例
        /// </summary>
        public static DiskSpaceDto CreateDefault()
        {
            return new DiskSpaceDto
            {
                UsedSpace = 0,
                TotalSpace = 100L * 1024 * 1024 * 1024, // 100GB
                AvailableSpace = 100L * 1024 * 1024 * 1024
            };
        }

        /// <summary>
        /// 创建测试数据
        /// </summary>
        public static DiskSpaceDto CreateTestData()
        {
            var totalSpace = 500L * 1024 * 1024 * 1024; // 500GB
            var usedSpace = 200L * 1024 * 1024 * 1024;  // 200GB
            var availableSpace = totalSpace - usedSpace;

            return new DiskSpaceDto
            {
                UsedSpace = usedSpace,
                TotalSpace = totalSpace,
                AvailableSpace = availableSpace
            };
        }

        public override string ToString()
        {
            return $"磁盘空间: {FormattedUsedSpace}/{FormattedTotalSpace} ({UsagePercentage:F1}%)";
        }
    }
}
