using System;

namespace VideoConversion_ClientTo.Application.DTOs
{
    /// <summary>
    /// 磁盘空间状态DTO
    /// 职责: 传输磁盘空间状态变化数据
    /// </summary>
    public class DiskSpaceStatusDto
    {
        /// <summary>
        /// 状态类型
        /// </summary>
        public string StatusType { get; set; } = string.Empty;

        /// <summary>
        /// 状态消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 当前使用空间（字节）
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
        /// 警告阈值（百分比）
        /// </summary>
        public double WarningThreshold { get; set; } = 80.0;

        /// <summary>
        /// 危险阈值（百分比）
        /// </summary>
        public double DangerThreshold { get; set; } = 90.0;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 是否为警告状态
        /// </summary>
        public bool IsWarning => UsagePercentage >= WarningThreshold && UsagePercentage < DangerThreshold;

        /// <summary>
        /// 是否为危险状态
        /// </summary>
        public bool IsDanger => UsagePercentage >= DangerThreshold;

        /// <summary>
        /// 是否为正常状态
        /// </summary>
        public bool IsNormal => UsagePercentage < WarningThreshold;

        /// <summary>
        /// 状态级别
        /// </summary>
        public string StatusLevel
        {
            get
            {
                if (IsDanger) return "Danger";
                if (IsWarning) return "Warning";
                return "Normal";
            }
        }

        /// <summary>
        /// 状态图标
        /// </summary>
        public string StatusIcon
        {
            get
            {
                return StatusLevel switch
                {
                    "Danger" => "🔴",
                    "Warning" => "🟡",
                    "Normal" => "🟢",
                    _ => "⚪"
                };
            }
        }

        /// <summary>
        /// 格式化的使用空间
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
        /// 格式化的使用率
        /// </summary>
        public string FormattedUsagePercentage => $"{UsagePercentage:F1}%";

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
        /// 创建正常状态
        /// </summary>
        public static DiskSpaceStatusDto CreateNormal(long usedSpace, long totalSpace)
        {
            return new DiskSpaceStatusDto
            {
                StatusType = "Normal",
                Message = "磁盘空间充足",
                UsedSpace = usedSpace,
                TotalSpace = totalSpace,
                AvailableSpace = totalSpace - usedSpace,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 创建警告状态
        /// </summary>
        public static DiskSpaceStatusDto CreateWarning(long usedSpace, long totalSpace)
        {
            return new DiskSpaceStatusDto
            {
                StatusType = "Warning",
                Message = "磁盘空间使用率较高，建议清理",
                UsedSpace = usedSpace,
                TotalSpace = totalSpace,
                AvailableSpace = totalSpace - usedSpace,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 创建危险状态
        /// </summary>
        public static DiskSpaceStatusDto CreateDanger(long usedSpace, long totalSpace)
        {
            return new DiskSpaceStatusDto
            {
                StatusType = "Danger",
                Message = "磁盘空间不足，请立即清理",
                UsedSpace = usedSpace,
                TotalSpace = totalSpace,
                AvailableSpace = totalSpace - usedSpace,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public override string ToString()
        {
            return $"磁盘空间状态: {StatusLevel} - {FormattedUsedSpace}/{FormattedTotalSpace} ({FormattedUsagePercentage})";
        }
    }
}
