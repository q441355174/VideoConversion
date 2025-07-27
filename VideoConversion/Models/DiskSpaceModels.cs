using System.ComponentModel.DataAnnotations;
using SqlSugar;

namespace VideoConversion.Models
{
    /// <summary>
    /// 磁盘空间配置模型
    /// </summary>
    public class DiskSpaceConfig
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        
        /// <summary>
        /// 最大总占用空间(字节)
        /// </summary>
        public long MaxTotalSpace { get; set; }
        
        /// <summary>
        /// 保留空间(字节) - 默认5GB
        /// </summary>
        public long ReservedSpace { get; set; } = 5L * 1024 * 1024 * 1024;
        
        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 更新者
        /// </summary>
        public string UpdatedBy { get; set; } = "System";
        
        /// <summary>
        /// 是否启用空间限制
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// 空间使用统计模型
    /// </summary>
    public class SpaceUsage
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        
        /// <summary>
        /// 已上传文件大小(字节)
        /// </summary>
        public long UploadedFilesSize { get; set; }
        
        /// <summary>
        /// 已转换文件大小(字节)
        /// </summary>
        public long ConvertedFilesSize { get; set; }
        
        /// <summary>
        /// 临时文件大小(字节)
        /// </summary>
        public long TempFilesSize { get; set; }
        
        /// <summary>
        /// 最后计算时间
        /// </summary>
        public DateTime LastCalculatedAt { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 总使用空间
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public long TotalUsedSpace => UploadedFilesSize + ConvertedFilesSize + TempFilesSize;
    }

    /// <summary>
    /// 磁盘空间状态
    /// </summary>
    public class DiskSpaceStatus
    {
        /// <summary>
        /// 总配置空间
        /// </summary>
        public long TotalSpace { get; set; }
        
        /// <summary>
        /// 已使用空间
        /// </summary>
        public long UsedSpace { get; set; }
        
        /// <summary>
        /// 可用空间
        /// </summary>
        public long AvailableSpace { get; set; }
        
        /// <summary>
        /// 保留空间
        /// </summary>
        public long ReservedSpace { get; set; }
        
        /// <summary>
        /// 是否有足够空间
        /// </summary>
        public bool HasSufficientSpace { get; set; }
        
        /// <summary>
        /// 最小所需空间
        /// </summary>
        public long MinRequiredSpace { get; set; }
        
        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 使用率百分比
        /// </summary>
        public double UsagePercentage => TotalSpace > 0 ? (double)UsedSpace / TotalSpace * 100 : 0;
    }

    /// <summary>
    /// 空间检查请求
    /// </summary>
    public class SpaceCheckRequest
    {
        /// <summary>
        /// 原始文件大小
        /// </summary>
        [Required]
        public long OriginalFileSize { get; set; }
        
        /// <summary>
        /// 预估输出文件大小
        /// </summary>
        public long? EstimatedOutputSize { get; set; }
        
        /// <summary>
        /// 任务类型
        /// </summary>
        public string TaskType { get; set; } = "conversion";
        
        /// <summary>
        /// 是否包含临时文件空间
        /// </summary>
        public bool IncludeTempSpace { get; set; } = true;
    }

    /// <summary>
    /// 空间检查响应
    /// </summary>
    public class SpaceCheckResponse
    {
        /// <summary>
        /// 是否有足够空间
        /// </summary>
        public bool HasEnoughSpace { get; set; }
        
        /// <summary>
        /// 所需空间
        /// </summary>
        public long RequiredSpace { get; set; }
        
        /// <summary>
        /// 可用空间
        /// </summary>
        public long AvailableSpace { get; set; }
        
        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; } = "";
        
        /// <summary>
        /// 详细信息
        /// </summary>
        public SpaceCheckDetails? Details { get; set; }
    }

    /// <summary>
    /// 空间检查详细信息
    /// </summary>
    public class SpaceCheckDetails
    {
        /// <summary>
        /// 原始文件空间
        /// </summary>
        public long OriginalFileSpace { get; set; }
        
        /// <summary>
        /// 输出文件空间
        /// </summary>
        public long OutputFileSpace { get; set; }
        
        /// <summary>
        /// 临时文件空间
        /// </summary>
        public long TempFileSpace { get; set; }
        
        /// <summary>
        /// 保留空间
        /// </summary>
        public long ReservedSpace { get; set; }
        
        /// <summary>
        /// 当前使用空间
        /// </summary>
        public long CurrentUsedSpace { get; set; }
        
        /// <summary>
        /// 总配置空间
        /// </summary>
        public long TotalConfiguredSpace { get; set; }
    }

    /// <summary>
    /// 磁盘空间配置请求
    /// </summary>
    public class DiskSpaceConfigRequest
    {
        /// <summary>
        /// 最大总空间(GB)
        /// </summary>
        [Required]
        [Range(1, 10000, ErrorMessage = "空间配置必须在1GB到10000GB之间")]
        public double MaxTotalSpaceGB { get; set; }
        
        /// <summary>
        /// 保留空间(GB)
        /// </summary>
        [Range(1, 100, ErrorMessage = "保留空间必须在1GB到100GB之间")]
        public double ReservedSpaceGB { get; set; } = 5.0;
        
        /// <summary>
        /// 是否启用空间限制
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// 空间类别枚举
    /// </summary>
    public enum SpaceCategory
    {
        OriginalFiles,  // 原始文件
        ConvertedFiles, // 转换后文件
        TempFiles       // 临时文件
    }

    /// <summary>
    /// 批量任务控制动作
    /// </summary>
    public enum BatchTaskAction
    {
        Pause,    // 暂停
        Resume,   // 恢复
        Continue  // 继续
    }

    /// <summary>
    /// 批量任务控制
    /// </summary>
    public class BatchTaskControl
    {
        /// <summary>
        /// 批量任务ID
        /// </summary>
        public string BatchId { get; set; } = "";
        
        /// <summary>
        /// 控制动作
        /// </summary>
        public BatchTaskAction Action { get; set; }
        
        /// <summary>
        /// 原因
        /// </summary>
        public string Reason { get; set; } = "";
        
        /// <summary>
        /// 所需空间
        /// </summary>
        public long RequiredSpace { get; set; }
        
        /// <summary>
        /// 可用空间
        /// </summary>
        public long AvailableSpace { get; set; }
        
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
