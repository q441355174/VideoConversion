using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Domain.Entities;
using VideoConversion_ClientTo.Domain.ValueObjects;
using VideoConversion_ClientTo.Application.DTOs;

namespace VideoConversion_ClientTo.Application.Interfaces
{
    /// <summary>
    /// STEP-5: 转换任务服务接口
    /// 职责: 定义转换任务相关的业务操作
    /// </summary>
    public interface IConversionTaskService
    {
        #region 任务管理

        /// <summary>
        /// 创建新的转换任务
        /// </summary>
        Task<ConversionTask> CreateTaskAsync(
            string taskName,
            string filePath,
            long fileSize,
            ConversionParameters parameters);

        /// <summary>
        /// 获取任务详情
        /// </summary>
        Task<ConversionTask?> GetTaskAsync(TaskId taskId);

        /// <summary>
        /// 获取所有任务
        /// </summary>
        Task<IEnumerable<ConversionTask>> GetAllTasksAsync();

        /// <summary>
        /// 获取已完成的任务
        /// </summary>
        Task<IEnumerable<ConversionTask>> GetCompletedTasksAsync(int page = 1, int pageSize = 50);

        /// <summary>
        /// 获取正在进行的任务
        /// </summary>
        Task<IEnumerable<ConversionTask>> GetActiveTasksAsync();

        /// <summary>
        /// 删除任务
        /// </summary>
        Task<bool> DeleteTaskAsync(TaskId taskId);

        #endregion

        #region 任务操作

        /// <summary>
        /// 开始转换任务
        /// </summary>
        Task<bool> StartConversionAsync(TaskId taskId, StartConversionRequestDto request);

        /// <summary>
        /// 取消转换任务
        /// </summary>
        Task<bool> CancelTaskAsync(TaskId taskId);

        /// <summary>
        /// 重试失败的任务
        /// </summary>
        Task<bool> RetryTaskAsync(TaskId taskId);

        #endregion

        #region 进度管理

        /// <summary>
        /// 更新任务进度
        /// </summary>
        Task UpdateTaskProgressAsync(TaskId taskId, int progress, double? speed = null, TimeSpan? estimatedRemaining = null);

        /// <summary>
        /// 标记任务完成
        /// </summary>
        Task CompleteTaskAsync(TaskId taskId);

        /// <summary>
        /// 标记任务失败
        /// </summary>
        Task FailTaskAsync(TaskId taskId, string errorMessage);

        #endregion

        #region 文件操作

        /// <summary>
        /// 下载转换后的文件
        /// </summary>
        Task<string?> DownloadTaskFileAsync(TaskId taskId);

        /// <summary>
        /// 检查本地文件是否存在
        /// </summary>
        Task<bool> HasLocalFileAsync(TaskId taskId);

        /// <summary>
        /// 获取本地文件路径
        /// </summary>
        Task<string?> GetLocalFilePathAsync(TaskId taskId);

        #endregion

        #region 事件

        /// <summary>
        /// 任务状态变更事件
        /// </summary>
        event EventHandler<TaskStatusChangedEventArgs>? TaskStatusChanged;

        /// <summary>
        /// 任务进度更新事件
        /// </summary>
        event EventHandler<TaskProgressUpdatedEventArgs>? TaskProgressUpdated;

        /// <summary>
        /// 任务完成事件
        /// </summary>
        event EventHandler<TaskCompletedEventArgs>? TaskCompleted;

        #endregion
    }

    /// <summary>
    /// STEP-5: 任务状态变更事件参数
    /// </summary>
    public class TaskStatusChangedEventArgs : EventArgs
    {
        public TaskStatusChangedEventArgs(TaskId taskId, Domain.Enums.TaskStatus oldStatus, Domain.Enums.TaskStatus newStatus)
        {
            TaskId = taskId;
            OldStatus = oldStatus;
            NewStatus = newStatus;
        }

        public TaskId TaskId { get; }
        public Domain.Enums.TaskStatus OldStatus { get; }
        public Domain.Enums.TaskStatus NewStatus { get; }
    }

    /// <summary>
    /// STEP-5: 任务进度更新事件参数
    /// </summary>
    public class TaskProgressUpdatedEventArgs : EventArgs
    {
        public TaskProgressUpdatedEventArgs(TaskId taskId, int progress, double? speed = null, TimeSpan? estimatedRemaining = null)
        {
            TaskId = taskId;
            Progress = progress;
            Speed = speed;
            EstimatedRemaining = estimatedRemaining;
        }

        public TaskId TaskId { get; }
        public int Progress { get; }
        public double? Speed { get; }
        public TimeSpan? EstimatedRemaining { get; }
    }

    /// <summary>
    /// STEP-5: 任务完成事件参数
    /// </summary>
    public class TaskCompletedEventArgs : EventArgs
    {
        public TaskCompletedEventArgs(TaskId taskId, string taskName, bool success, string? errorMessage = null)
        {
            TaskId = taskId;
            TaskName = taskName;
            Success = success;
            ErrorMessage = errorMessage;
        }

        public TaskId TaskId { get; }
        public string TaskName { get; }
        public bool Success { get; }
        public string? ErrorMessage { get; }
    }
}
