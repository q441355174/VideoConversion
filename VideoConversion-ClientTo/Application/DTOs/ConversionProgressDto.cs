using System;

namespace VideoConversion_ClientTo.Application.DTOs
{
    /// <summary>
    /// 转换进度DTO
    /// 职责: 传输转换进度相关数据
    /// </summary>
    public class ConversionProgressDto
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
        /// 进度百分比 (0-100)
        /// </summary>
        public int Progress { get; set; }

        /// <summary>
        /// 转换速度倍数
        /// </summary>
        public double? Speed { get; set; }

        /// <summary>
        /// 预计剩余时间（秒）
        /// </summary>
        public double? EstimatedRemainingSeconds { get; set; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

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
        public string FormattedSpeed => Speed?.ToString("0.0x") ?? "";

        /// <summary>
        /// 格式化的预计剩余时间
        /// </summary>
        public string FormattedETA
        {
            get
            {
                if (!EstimatedRemainingSeconds.HasValue)
                    return "";

                var eta = TimeSpan.FromSeconds(EstimatedRemainingSeconds.Value);
                if (eta.TotalHours >= 1)
                    return $"{eta:h\\:mm\\:ss}";
                else
                    return $"{eta:mm\\:ss}";
            }
        }

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted => Progress >= 100;

        /// <summary>
        /// 是否有错误
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// 创建默认实例
        /// </summary>
        public static ConversionProgressDto CreateDefault(string taskId)
        {
            return new ConversionProgressDto
            {
                TaskId = taskId,
                TaskName = $"任务 {taskId}",
                Progress = 0,
                Status = "等待中",
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 创建测试数据
        /// </summary>
        public static ConversionProgressDto CreateTestData(string taskId, int progress)
        {
            return new ConversionProgressDto
            {
                TaskId = taskId,
                TaskName = $"测试任务 {taskId}",
                Progress = progress,
                Speed = 1.2,
                EstimatedRemainingSeconds = (100 - progress) * 2.5, // 假设每1%需要2.5秒
                Status = progress < 100 ? "转换中" : "已完成",
                UpdatedAt = DateTime.UtcNow
            };
        }

        public override string ToString()
        {
            return $"任务 {TaskId}: {FormattedProgress} - {FormattedSpeed} - ETA: {FormattedETA}";
        }
    }
}
