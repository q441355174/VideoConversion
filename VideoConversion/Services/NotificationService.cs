using Microsoft.AspNetCore.SignalR;
using VideoConversion.Hubs;
using VideoConversion.Models;

namespace VideoConversion.Services
{
    /// <summary>
    /// 统一通知服务 - 集中管理所有SignalR通知逻辑
    /// </summary>
    public class NotificationService
    {
        private readonly IHubContext<ConversionHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IHubContext<ConversionHub> hubContext,
            ILogger<NotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// 发送进度更新通知
        /// </summary>
        public async Task NotifyProgressAsync(string taskId, int progress, string message, double? speed = null, int? remainingSeconds = null)
        {
            try
            {
                var progressData = new
                {
                    TaskId = taskId,
                    Progress = progress,
                    Message = message,
                    Speed = speed,
                    RemainingSeconds = remainingSeconds,
                    Timestamp = DateTime.Now
                };

                _logger.LogInformation("📡 发送进度更新: TaskId={TaskId}, Progress={Progress}%, Group=task_{TaskId}",
                    taskId, progress, taskId);

                await _hubContext.Clients.Group($"task_{taskId}").SendAsync("ProgressUpdate", progressData);

                _logger.LogDebug("✅ 进度更新已发送: TaskId={TaskId}, Progress={Progress}%", taskId, progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 发送进度更新通知失败: {TaskId} - {Progress}%", taskId, progress);
            }
        }

        /// <summary>
        /// 发送任务状态变化通知
        /// </summary>
        public async Task NotifyStatusChangeAsync(string taskId, ConversionStatus status, string? errorMessage = null)
        {
            try
            {
                await _hubContext.Clients.Group($"task_{taskId}").SendAsync("StatusUpdate", new
                {
                    TaskId = taskId,
                    Status = status.ToString(),
                    ErrorMessage = errorMessage,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送任务状态变化通知失败: {TaskId} - {Status}", taskId, status);
            }
        }

        /// <summary>
        /// 发送任务完成通知
        /// </summary>
        public async Task NotifyTaskCompletedAsync(string taskId, bool success, string? errorMessage = null, string? outputFileName = null)
        {
            try
            {
                await _hubContext.Clients.Group($"task_{taskId}").SendAsync("TaskCompleted", new
                {
                    TaskId = taskId,
                    Success = success,
                    ErrorMessage = errorMessage,
                    OutputFileName = outputFileName,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送任务完成通知失败: {TaskId}", taskId);
            }
        }

        /// <summary>
        /// 发送系统通知
        /// </summary>
        public async Task NotifySystemAsync(string message, string type = "info")
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("SystemNotification", new
                {
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送系统通知失败: {Message}", message);
            }
        }

        /// <summary>
        /// 发送任务状态响应（用于客户端查询）
        /// </summary>
        public async Task SendTaskStatusAsync(string taskId, string status, string? errorMessage = null)
        {
            try
            {
                await _hubContext.Clients.Group($"task_{taskId}").SendAsync("TaskStatus", new
                {
                    TaskId = taskId,
                    Status = status,
                    ErrorMessage = errorMessage,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送任务状态响应失败: {TaskId} - {Status}", taskId, status);
            }
        }

        /// <summary>
        /// 通知任务未找到
        /// </summary>
        public async Task NotifyTaskNotFoundAsync(string connectionId, string taskId)
        {
            try
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("TaskNotFound", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送任务未找到通知失败: {TaskId}", taskId);
            }
        }

        /// <summary>
        /// 发送错误通知
        /// </summary>
        public async Task NotifyErrorAsync(string connectionId, string message)
        {
            try
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("Error", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送错误通知失败: {Message}", message);
            }
        }
    }
}
