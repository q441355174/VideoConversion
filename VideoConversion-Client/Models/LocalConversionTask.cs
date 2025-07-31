using System;
using SqlSugar;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Models
{
    /// <summary>
    /// 本地转换任务模型 - 客户端任务管理的核心数据结构
    /// </summary>
    [SugarTable("LocalConversionTasks")]
    public class LocalConversionTask
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
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }
        
        // 🔑 转换参数（与服务器ConversionTask对应）
        /// <summary>
        /// 输出格式
        /// </summary>
        public string OutputFormat { get; set; } = "";
        
        /// <summary>
        /// 目标分辨率
        /// </summary>
        public string Resolution { get; set; } = "";
        
        /// <summary>
        /// 视频编码器
        /// </summary>
        public string VideoCodec { get; set; } = "";
        
        /// <summary>
        /// 音频编码器
        /// </summary>
        public string AudioCodec { get; set; } = "";
        
        /// <summary>
        /// 视频质量
        /// </summary>
        public string VideoQuality { get; set; } = "";
        
        /// <summary>
        /// 音频质量
        /// </summary>
        public string AudioQuality { get; set; } = "";
        
        /// <summary>
        /// 编码预设
        /// </summary>
        public string EncodingPreset { get; set; } = "";
        
        /// <summary>
        /// 质量模式
        /// </summary>
        public string QualityMode { get; set; } = "";
        
        /// <summary>
        /// 是否使用两遍编码
        /// </summary>
        public bool TwoPass { get; set; } = false;
        
        /// <summary>
        /// 是否启用快速启动
        /// </summary>
        public bool FastStart { get; set; } = true;
        
        // 🔑 状态和进度管理
        /// <summary>
        /// 转换状态
        /// </summary>
        public ConversionStatus Status { get; set; } = ConversionStatus.Pending;
        
        /// <summary>
        /// 进度百分比 (0-100)
        /// </summary>
        public int Progress { get; set; } = 0;
        
        /// <summary>
        /// 当前阶段 (pending, uploading, converting, completed, failed)
        /// </summary>
        public string CurrentPhase { get; set; } = "pending";
        
        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; } = "";
        
        // 🔑 时间戳追踪
        /// <summary>
        /// 本地创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 服务器任务创建时间
        /// </summary>
        public DateTime? ServerTaskCreatedAt { get; set; }
        
        /// <summary>
        /// 任务开始时间
        /// </summary>
        public DateTime? StartedAt { get; set; }
        
        /// <summary>
        /// 上传开始时间
        /// </summary>
        public DateTime? UploadStartedAt { get; set; }
        
        /// <summary>
        /// 上传完成时间
        /// </summary>
        public DateTime? UploadCompletedAt { get; set; }
        
        /// <summary>
        /// 转换开始时间
        /// </summary>
        public DateTime? ConversionStartedAt { get; set; }
        
        /// <summary>
        /// 任务完成时间
        /// </summary>
        public DateTime? CompletedAt { get; set; }
        
        // 🔑 下载和文件管理
        /// <summary>
        /// 下载URL
        /// </summary>
        public string? DownloadUrl { get; set; }
        
        /// <summary>
        /// 本地输出文件路径
        /// </summary>
        public string? LocalOutputPath { get; set; }
        
        /// <summary>
        /// 是否已下载
        /// </summary>
        public bool IsDownloaded { get; set; } = false;
        
        /// <summary>
        /// 下载完成时间
        /// </summary>
        public DateTime? DownloadedAt { get; set; }
        
        /// <summary>
        /// 输出文件大小
        /// </summary>
        public long OutputFileSize { get; set; } = 0;
        
        // 🔑 源文件处理
        /// <summary>
        /// 源文件是否已处理
        /// </summary>
        public bool SourceFileProcessed { get; set; } = false;
        
        /// <summary>
        /// 源文件处理动作 (keep, delete, archive)
        /// </summary>
        public string SourceFileAction { get; set; } = "keep";
        
        /// <summary>
        /// 归档路径
        /// </summary>
        public string? ArchivePath { get; set; }
        
        /// <summary>
        /// 源文件处理时间
        /// </summary>
        public DateTime? SourceFileProcessedAt { get; set; }
        
        // 🔑 元数据和设置
        /// <summary>
        /// 原始文件元数据 (JSON格式)
        /// </summary>
        public string? OriginalMetadata { get; set; }
        
        /// <summary>
        /// 转换设置 (JSON格式)
        /// </summary>
        public string? ConversionSettings { get; set; }
        
        /// <summary>
        /// 进度历史记录 (JSON格式)
        /// </summary>
        public string? ProgressHistory { get; set; }
        
        // 🔑 性能统计
        /// <summary>
        /// 转换速度倍率
        /// </summary>
        public double? ConversionSpeed { get; set; }
        
        /// <summary>
        /// 预计剩余时间（秒）
        /// </summary>
        public int? EstimatedTimeRemaining { get; set; }
        
        /// <summary>
        /// 视频时长（秒）
        /// </summary>
        public double? Duration { get; set; }
        
        /// <summary>
        /// 当前转换时间（秒）
        /// </summary>
        public double? CurrentTime { get; set; }
        
        // 🔑 错误处理和重试
        /// <summary>
        /// 重试次数
        /// </summary>
        public int RetryCount { get; set; } = 0;
        
        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetries { get; set; } = 3;
        
        /// <summary>
        /// 最后一次错误信息
        /// </summary>
        public string? LastError { get; set; }
        
        /// <summary>
        /// 最后一次重试时间
        /// </summary>
        public DateTime? LastRetryAt { get; set; }
        
        // 🔑 事务和批量处理标识
        /// <summary>
        /// 事务ID
        /// </summary>
        public string? TransactionId { get; set; }
        
        /// <summary>
        /// 批量处理ID
        /// </summary>
        public string? BatchProcessingId { get; set; }
        
        /// <summary>
        /// 获取格式化的文件大小
        /// </summary>
        public string FormattedFileSize => FormatFileSize(FileSize);
        
        /// <summary>
        /// 获取格式化的输出文件大小
        /// </summary>
        public string FormattedOutputFileSize => FormatFileSize(OutputFileSize);
        
        /// <summary>
        /// 获取任务持续时间
        /// </summary>
        public TimeSpan? TaskDuration
        {
            get
            {
                if (StartedAt.HasValue && CompletedAt.HasValue)
                {
                    return CompletedAt.Value - StartedAt.Value;
                }
                return null;
            }
        }
        
        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private static string FormatFileSize(long bytes)
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
        /// 获取状态显示文本
        /// </summary>
        public string GetStatusDisplayText()
        {
            return Status switch
            {
                ConversionStatus.Pending => "等待处理",
                ConversionStatus.Uploading => $"上传中 {Progress}%",
                ConversionStatus.Converting => $"转换中 {Progress}%",
                ConversionStatus.Completed => "转换完成",
                ConversionStatus.Failed => $"失败: {ErrorMessage}",
                ConversionStatus.Cancelled => "已取消",
                _ => "未知状态"
            };
        }
    }
}
