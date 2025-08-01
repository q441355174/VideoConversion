using System;

namespace VideoConversion_ClientTo.Application.DTOs
{
    /// <summary>
    /// 任务完成DTO
    /// 职责: 传输任务完成相关数据
    /// </summary>
    public class TaskCompletedDto
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// 任务名称
        /// </summary>
        public string TaskName { get; set; } = string.Empty;

        /// <summary>
        /// 源文件名
        /// </summary>
        public string SourceFileName { get; set; } = string.Empty;

        /// <summary>
        /// 输出文件名
        /// </summary>
        public string OutputFileName { get; set; } = string.Empty;

        /// <summary>
        /// 输出格式
        /// </summary>
        public string OutputFormat { get; set; } = string.Empty;

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 输出文件大小（字节）
        /// </summary>
        public long OutputFileSize { get; set; }

        /// <summary>
        /// 转换开始时间
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// 转换完成时间
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 转换耗时
        /// </summary>
        public TimeSpan Duration => CompletedAt - StartedAt;

        /// <summary>
        /// 平均转换速度
        /// </summary>
        public double AverageSpeed { get; set; }

        /// <summary>
        /// 压缩比例
        /// </summary>
        public double CompressionRatio => FileSize > 0 ? (double)OutputFileSize / FileSize : 1.0;

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; } = true;

        /// <summary>
        /// 错误消息（如果有）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 下载URL
        /// </summary>
        public string? DownloadUrl { get; set; }

        /// <summary>
        /// 格式化的文件大小
        /// </summary>
        public string FormattedFileSize => FormatFileSize(FileSize);

        /// <summary>
        /// 格式化的输出文件大小
        /// </summary>
        public string FormattedOutputFileSize => FormatFileSize(OutputFileSize);

        /// <summary>
        /// 格式化的转换耗时
        /// </summary>
        public string FormattedDuration
        {
            get
            {
                if (Duration.TotalHours >= 1)
                    return $"{Duration:h\\:mm\\:ss}";
                else
                    return $"{Duration:mm\\:ss}";
            }
        }

        /// <summary>
        /// 格式化的压缩比例
        /// </summary>
        public string FormattedCompressionRatio => $"{CompressionRatio:P1}";

        /// <summary>
        /// 格式化的平均速度
        /// </summary>
        public string FormattedAverageSpeed => $"{AverageSpeed:0.0}x";

        /// <summary>
        /// 完成状态图标
        /// </summary>
        public string StatusIcon => IsSuccess ? "✅" : "❌";

        /// <summary>
        /// 完成状态文本
        /// </summary>
        public string StatusText => IsSuccess ? "转换成功" : "转换失败";

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
        /// 创建成功完成的任务
        /// </summary>
        public static TaskCompletedDto CreateSuccess(string taskId, string taskName, string sourceFileName, string outputFileName)
        {
            return new TaskCompletedDto
            {
                TaskId = taskId,
                TaskName = taskName,
                SourceFileName = sourceFileName,
                OutputFileName = outputFileName,
                IsSuccess = true,
                CompletedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 创建失败的任务
        /// </summary>
        public static TaskCompletedDto CreateFailure(string taskId, string taskName, string sourceFileName, string errorMessage)
        {
            return new TaskCompletedDto
            {
                TaskId = taskId,
                TaskName = taskName,
                SourceFileName = sourceFileName,
                IsSuccess = false,
                ErrorMessage = errorMessage,
                CompletedAt = DateTime.UtcNow
            };
        }

        public override string ToString()
        {
            return $"任务完成: {TaskName} - {StatusText} ({FormattedDuration})";
        }
    }
}
