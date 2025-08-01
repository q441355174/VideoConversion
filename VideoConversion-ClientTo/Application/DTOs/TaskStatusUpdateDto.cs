using System;

namespace VideoConversion_ClientTo.Application.DTOs
{
    /// <summary>
    /// 任务状态更新DTO
    /// 职责: 传输任务状态变化数据
    /// </summary>
    public class TaskStatusUpdateDto
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
        /// 旧状态
        /// </summary>
        public string OldStatus { get; set; } = string.Empty;

        /// <summary>
        /// 新状态
        /// </summary>
        public string NewStatus { get; set; } = string.Empty;

        /// <summary>
        /// 状态变化原因
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// 错误消息（如果有）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 是否为错误状态
        /// </summary>
        public bool IsError => NewStatus == "Failed" || NewStatus == "Error";

        /// <summary>
        /// 是否为完成状态
        /// </summary>
        public bool IsCompleted => NewStatus == "Completed";

        /// <summary>
        /// 是否为进行中状态
        /// </summary>
        public bool IsInProgress => NewStatus == "InProgress" || NewStatus == "Converting" || NewStatus == "Uploading";

        /// <summary>
        /// 状态显示文本
        /// </summary>
        public string StatusDisplay
        {
            get
            {
                return NewStatus switch
                {
                    "Pending" => "等待中",
                    "Uploading" => "上传中",
                    "Converting" => "转换中",
                    "InProgress" => "进行中",
                    "Completed" => "已完成",
                    "Failed" => "失败",
                    "Error" => "错误",
                    "Cancelled" => "已取消",
                    _ => NewStatus
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
                return NewStatus switch
                {
                    "Pending" => "⏳",
                    "Uploading" => "📤",
                    "Converting" => "⚙️",
                    "InProgress" => "🔄",
                    "Completed" => "✅",
                    "Failed" => "❌",
                    "Error" => "⚠️",
                    "Cancelled" => "⏹️",
                    _ => "❓"
                };
            }
        }

        /// <summary>
        /// 创建状态更新实例
        /// </summary>
        public static TaskStatusUpdateDto Create(string taskId, string taskName, string oldStatus, string newStatus, string? reason = null)
        {
            return new TaskStatusUpdateDto
            {
                TaskId = taskId,
                TaskName = taskName,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Reason = reason,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public override string ToString()
        {
            return $"任务状态更新: {TaskName} ({OldStatus} → {NewStatus})";
        }
    }
}
