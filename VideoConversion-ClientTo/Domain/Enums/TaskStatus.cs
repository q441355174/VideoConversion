namespace VideoConversion_ClientTo.Domain.Enums
{
    /// <summary>
    /// STEP-1: 领域枚举 - 任务状态
    /// 职责: 定义任务生命周期中的所有可能状态
    /// </summary>
    public enum TaskStatus
    {
        /// <summary>
        /// 等待开始
        /// </summary>
        Pending = 0,

        /// <summary>
        /// 正在转换
        /// </summary>
        Converting = 1,

        /// <summary>
        /// 转换完成
        /// </summary>
        Completed = 2,

        /// <summary>
        /// 转换失败
        /// </summary>
        Failed = 3,

        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled = 4
    }

    /// <summary>
    /// STEP-1: 任务状态扩展方法
    /// 职责: 提供状态相关的业务逻辑
    /// </summary>
    public static class TaskStatusExtensions
    {
        public static bool IsTerminal(this TaskStatus status)
        {
            return status == TaskStatus.Completed || 
                   status == TaskStatus.Failed || 
                   status == TaskStatus.Cancelled;
        }

        public static bool CanTransitionTo(this TaskStatus from, TaskStatus to)
        {
            return from switch
            {
                TaskStatus.Pending => to == TaskStatus.Converting || to == TaskStatus.Cancelled,
                TaskStatus.Converting => to == TaskStatus.Completed || to == TaskStatus.Failed || to == TaskStatus.Cancelled,
                TaskStatus.Completed => false,
                TaskStatus.Failed => false,
                TaskStatus.Cancelled => false,
                _ => false
            };
        }

        public static string GetDisplayName(this TaskStatus status)
        {
            return status switch
            {
                TaskStatus.Pending => "等待中",
                TaskStatus.Converting => "转换中",
                TaskStatus.Completed => "已完成",
                TaskStatus.Failed => "失败",
                TaskStatus.Cancelled => "已取消",
                _ => "未知"
            };
        }

        public static string GetStatusIcon(this TaskStatus status)
        {
            return status switch
            {
                TaskStatus.Pending => "⏳",
                TaskStatus.Converting => "🔄",
                TaskStatus.Completed => "✅",
                TaskStatus.Failed => "❌",
                TaskStatus.Cancelled => "⏹️",
                _ => "❓"
            };
        }
    }
}
