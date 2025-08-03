using SqlSugar;
using System;

namespace VideoConversion_ClientTo.Infrastructure.Data.Entities
{
    /// <summary>
    /// 本地转换任务模型 - 与Client项目完全一致
    /// </summary>
    [SugarTable("LocalConversionTasks")]
    public class LocalConversionTaskEntity
    {
        // 🔑 核心标识符系统
        [SugarColumn(IsPrimaryKey = true)]
        public string LocalId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 服务器返回的TaskId
        /// </summary>
        public string? ServerTaskId { get; set; }

        /// <summary>
        /// 当前使用的TaskId（本地或服务器）
        /// </summary>
        public string CurrentTaskId { get; set; } = "";

        /// <summary>
        /// 批量任务ID
        /// </summary>
        public string? BatchId { get; set; }


        // 🔑 文件信息
        /// <summary>
        /// 文件完整路径
        /// </summary>
        public string FilePath { get; set; } = "";

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; } = "";

        /// <summary>
        /// 文件大小
        /// </summary>
        public long FileSize { get; set; } = 0;

        /// <summary>
        /// 文件扩展名
        /// </summary>
        public string FileExtension { get; set; } = "";

        /// <summary>
        /// 文件MIME类型
        /// </summary>
        public string? MimeType { get; set; }

        // 🔑 任务状态和进度
        /// <summary>
        /// 任务状态
        /// </summary>
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// 进度百分比 (0-100)
        /// </summary>
        public int Progress { get; set; } = 0;

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 详细错误信息
        /// </summary>
        public string? DetailedError { get; set; }


        // 🔑 转换配置
        /// <summary>
        /// 输出格式
        /// </summary>
        public string? OutputFormat { get; set; }

        /// <summary>
        /// 输出路径
        /// </summary>
        public string? OutputPath { get; set; }

        /// <summary>
        /// 转换参数（JSON格式）
        /// </summary>
        public string? ConversionParameters { get; set; }

        // 🔑 时间戳
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 服务器任务创建时间
        /// </summary>
        public DateTime? ServerTaskCreatedAt { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// 完成时间
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        // 🔑 性能和统计
        /// <summary>
        /// 转换速度
        /// </summary>
        public double? Speed { get; set; }

        /// <summary>
        /// 预计剩余时间（秒）
        /// </summary>
        public double? EstimatedRemainingSeconds { get; set; }

        /// <summary>
        /// 重试次数
        /// </summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        // 🔑 文件管理
        /// <summary>
        /// 本地文件路径（下载后的路径）
        /// </summary>
        public string? LocalFilePath { get; set; }

        /// <summary>
        /// 是否有本地文件
        /// </summary>
        public bool HasLocalFile { get; set; } = false;

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted => Status == "Completed" || Status == "Failed";

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
