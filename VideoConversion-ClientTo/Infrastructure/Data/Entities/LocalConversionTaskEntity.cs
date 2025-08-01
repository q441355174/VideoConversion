using System;
using System.ComponentModel.DataAnnotations;

namespace VideoConversion_ClientTo.Infrastructure.Data.Entities
{
    /// <summary>
    /// 本地转换任务实体
    /// 职责: 本地数据库中的任务记录
    /// </summary>
    public class LocalConversionTaskEntity
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 任务ID（来自服务器）
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// 任务名称
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string TaskName { get; set; } = string.Empty;

        /// <summary>
        /// 源文件路径
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string SourceFilePath { get; set; } = string.Empty;

        /// <summary>
        /// 源文件名
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string SourceFileName { get; set; } = string.Empty;

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 输出格式
        /// </summary>
        [MaxLength(10)]
        public string? OutputFormat { get; set; }

        /// <summary>
        /// 任务状态
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// 进度百分比
        /// </summary>
        public int Progress { get; set; } = 0;

        /// <summary>
        /// 转换速度
        /// </summary>
        public double? Speed { get; set; }

        /// <summary>
        /// 预计剩余时间（秒）
        /// </summary>
        public double? EstimatedRemainingSeconds { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 本地文件路径（下载后的路径）
        /// </summary>
        [MaxLength(500)]
        public string? LocalFilePath { get; set; }

        /// <summary>
        /// 是否有本地文件
        /// </summary>
        public bool HasLocalFile { get; set; } = false;

        /// <summary>
        /// 转换参数（JSON格式）
        /// </summary>
        [MaxLength(2000)]
        public string? ConversionParameters { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// 完成时间
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted => Status == "Completed";

        /// <summary>
        /// 是否正在进行
        /// </summary>
        public bool IsInProgress => Status == "InProgress" || Status == "Uploading" || Status == "Converting";

        /// <summary>
        /// 是否失败
        /// </summary>
        public bool IsFailed => Status == "Failed" || Status == "Error";

        /// <summary>
        /// 格式化的文件大小
        /// </summary>
        public string FormattedFileSize
        {
            get
            {
                if (FileSize == 0) return "0 B";

                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                int order = 0;
                double len = FileSize;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }

        /// <summary>
        /// 状态显示文本
        /// </summary>
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    "Pending" => "等待中",
                    "Uploading" => "上传中",
                    "Converting" => "转换中",
                    "Completed" => "已完成",
                    "Failed" => "失败",
                    "Error" => "错误",
                    "Cancelled" => "已取消",
                    _ => Status
                };
            }
        }

        /// <summary>
        /// 状态图标
        /// </summary>
        public string StatusIcon
        {
            get
            {
                return Status switch
                {
                    "Pending" => "⏳",
                    "Uploading" => "📤",
                    "Converting" => "⚙️",
                    "Completed" => "✅",
                    "Failed" => "❌",
                    "Error" => "⚠️",
                    "Cancelled" => "⏹️",
                    _ => "❓"
                };
            }
        }
    }
}
