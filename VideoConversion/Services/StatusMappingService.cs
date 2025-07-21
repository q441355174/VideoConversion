using VideoConversion.Models;

namespace VideoConversion.Services
{
    /// <summary>
    /// 状态映射服务 - 统一前后端状态表示
    /// </summary>
    public static class StatusMappingService
    {
        /// <summary>
        /// 将ConversionTask映射为统一的DTO格式
        /// </summary>
        /// <param name="task">转换任务</param>
        /// <returns>任务状态DTO</returns>
        public static TaskStatusDto MapToDto(ConversionTask task)
        {
            return new TaskStatusDto
            {
                Id = task.Id,
                TaskName = task.TaskName ?? "",
                Status = (int)task.Status,
                StatusText = GetStatusText(task.Status),
                Progress = task.Progress,
                ErrorMessage = task.ErrorMessage ?? "",
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                EstimatedTimeRemaining = task.EstimatedTimeRemaining,
                ConversionSpeed = task.ConversionSpeed,
                Duration = (int?)task.Duration,
                CurrentTime = (int?)task.CurrentTime,
                OriginalFileName = task.OriginalFileName ?? "",
                OutputFileName = task.OutputFileName ?? "",
                InputFormat = task.InputFormat ?? "",
                OutputFormat = task.OutputFormat ?? "",
                VideoCodec = task.VideoCodec ?? "",
                AudioCodec = task.AudioCodec ?? "",
                OriginalFileSize = task.OriginalFileSize,
                OutputFileSize = task.OutputFileSize,
                InputFilePath = task.OriginalFilePath ?? "",
                OutputFilePath = task.OutputFilePath ?? ""
            };
        }

        /// <summary>
        /// 将任务列表映射为DTO列表
        /// </summary>
        /// <param name="tasks">任务列表</param>
        /// <returns>DTO列表</returns>
        public static List<TaskStatusDto> MapToDto(IEnumerable<ConversionTask> tasks)
        {
            return tasks.Select(MapToDto).ToList();
        }

        /// <summary>
        /// 获取状态文本描述
        /// </summary>
        /// <param name="status">转换状态</param>
        /// <returns>状态文本</returns>
        public static string GetStatusText(ConversionStatus status)
        {
            return status switch
            {
                ConversionStatus.Pending => "等待中",
                ConversionStatus.Converting => "转换中",
                ConversionStatus.Completed => "已完成",
                ConversionStatus.Failed => "失败",
                ConversionStatus.Cancelled => "已取消",
                _ => "未知状态"
            };
        }

        /// <summary>
        /// 获取状态CSS类名
        /// </summary>
        /// <param name="status">转换状态</param>
        /// <returns>CSS类名</returns>
        public static string GetStatusCssClass(ConversionStatus status)
        {
            return status switch
            {
                ConversionStatus.Pending => "bg-secondary",
                ConversionStatus.Converting => "bg-primary",
                ConversionStatus.Completed => "bg-success",
                ConversionStatus.Failed => "bg-danger",
                ConversionStatus.Cancelled => "bg-warning",
                _ => "bg-secondary"
            };
        }

        /// <summary>
        /// 获取状态图标类名
        /// </summary>
        /// <param name="status">转换状态</param>
        /// <returns>图标类名</returns>
        public static string GetStatusIconClass(ConversionStatus status)
        {
            return status switch
            {
                ConversionStatus.Pending => "fas fa-clock",
                ConversionStatus.Converting => "fas fa-spinner fa-spin",
                ConversionStatus.Completed => "fas fa-check-circle",
                ConversionStatus.Failed => "fas fa-times-circle",
                ConversionStatus.Cancelled => "fas fa-ban",
                _ => "fas fa-question-circle"
            };
        }

        /// <summary>
        /// 创建简化的任务信息（用于列表显示）
        /// </summary>
        /// <param name="task">转换任务</param>
        /// <returns>简化的任务信息</returns>
        public static object CreateSimpleTaskInfo(ConversionTask task)
        {
            return new
            {
                id = task.Id,
                taskName = task.TaskName ?? "",
                status = (int)task.Status,
                statusText = GetStatusText(task.Status),
                progress = task.Progress,
                createdAt = task.CreatedAt,
                completedAt = task.CompletedAt,
                originalFileName = task.OriginalFileName ?? "",
                outputFileName = task.OutputFileName ?? "",
                inputFormat = task.InputFormat ?? "",
                outputFormat = task.OutputFormat ?? "",
                originalFileSize = task.OriginalFileSize,
                outputFileSize = task.OutputFileSize,
                errorMessage = task.ErrorMessage ?? ""
            };
        }

        /// <summary>
        /// 创建详细的任务信息（用于状态查询）
        /// </summary>
        /// <param name="task">转换任务</param>
        /// <returns>详细的任务信息</returns>
        public static object CreateDetailedTaskInfo(ConversionTask task)
        {
            return new
            {
                id = task.Id,
                taskName = task.TaskName ?? "",
                status = task.Status.ToString(), // 返回字符串状态，与前端getStatusBadge函数匹配
                statusText = GetStatusText(task.Status),
                progress = task.Progress,
                errorMessage = task.ErrorMessage ?? "",
                createdAt = task.CreatedAt,
                startedAt = task.StartedAt,
                completedAt = task.CompletedAt,
                estimatedTimeRemaining = task.EstimatedTimeRemaining,
                conversionSpeed = task.ConversionSpeed,
                duration = task.Duration,
                currentTime = task.CurrentTime,
                originalFileName = task.OriginalFileName ?? "",
                outputFileName = task.OutputFileName ?? "",
                inputFormat = task.InputFormat ?? "",
                outputFormat = task.OutputFormat ?? "",
                videoCodec = task.VideoCodec ?? "",
                audioCodec = task.AudioCodec ?? "",
                originalFileSize = task.OriginalFileSize,
                outputFileSize = task.OutputFileSize,
                inputFilePath = task.OriginalFilePath ?? "",
                outputFilePath = task.OutputFilePath ?? ""
            };
        }
    }

    /// <summary>
    /// 任务状态DTO - 统一的数据传输对象
    /// </summary>
    public class TaskStatusDto
    {
        public string Id { get; set; } = "";
        public string TaskName { get; set; } = "";
        public int Status { get; set; }
        public string StatusText { get; set; } = "";
        public int Progress { get; set; }
        public string ErrorMessage { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? EstimatedTimeRemaining { get; set; }
        public double? ConversionSpeed { get; set; }
        public int? Duration { get; set; }
        public int? CurrentTime { get; set; }
        public string OriginalFileName { get; set; } = "";
        public string OutputFileName { get; set; } = "";
        public string InputFormat { get; set; } = "";
        public string OutputFormat { get; set; } = "";
        public string VideoCodec { get; set; } = "";
        public string AudioCodec { get; set; } = "";
        public long? OriginalFileSize { get; set; }
        public long? OutputFileSize { get; set; }
        public string InputFilePath { get; set; } = "";
        public string OutputFilePath { get; set; } = "";
    }
}
