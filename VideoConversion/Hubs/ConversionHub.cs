using Microsoft.AspNetCore.SignalR;
using VideoConversion.Services;

namespace VideoConversion.Hubs
{
    /// <summary>
    /// 视频转换进度推送Hub
    /// </summary>
    public class ConversionHub : Hub
    {
        private readonly DatabaseService _databaseService;
        private readonly ILogger<ConversionHub> _logger;

        public ConversionHub(DatabaseService databaseService, ILogger<ConversionHub> logger)
        {
            _databaseService = databaseService;
            _logger = logger;
        }

        /// <summary>
        /// 客户端连接时
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("客户端连接: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// 客户端断开连接时
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("客户端断开连接: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// 加入任务组（用于接收特定任务的进度更新）
        /// </summary>
        public async Task JoinTaskGroup(string taskId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"task_{taskId}");
            _logger.LogInformation("📡 客户端 {ConnectionId} 加入任务组: {TaskId}", Context.ConnectionId, taskId);
        }

        /// <summary>
        /// 离开任务组
        /// </summary>
        public async Task LeaveTaskGroup(string taskId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"task_{taskId}");
            _logger.LogDebug("客户端 {ConnectionId} 离开任务组: {TaskId}", Context.ConnectionId, taskId);
        }

        /// <summary>
        /// 获取任务状态
        /// </summary>
        public async Task GetTaskStatus(string taskId)
        {
            try
            {
                var task = await _databaseService.GetTaskAsync(taskId);
                if (task != null)
                {
                    await Clients.Caller.SendAsync("TaskStatus", new
                    {
                        TaskId = task.Id,
                        Status = task.Status.ToString(),
                        Progress = task.Progress,
                        ErrorMessage = task.ErrorMessage,
                        CreatedAt = task.CreatedAt,
                        StartedAt = task.StartedAt,
                        CompletedAt = task.CompletedAt,
                        EstimatedTimeRemaining = task.EstimatedTimeRemaining,
                        ConversionSpeed = task.ConversionSpeed,
                        Duration = task.Duration,
                        CurrentTime = task.CurrentTime
                    });
                }
                else
                {
                    await Clients.Caller.SendAsync("TaskNotFound", taskId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务状态失败: {TaskId}", taskId);
                await Clients.Caller.SendAsync("Error", $"获取任务状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有活动任务
        /// </summary>
        public async Task GetActiveTasks()
        {
            try
            {
                var tasks = await _databaseService.GetActiveTasksAsync();
                var taskData = tasks.Select(t => new
                {
                    TaskId = t.Id,
                    TaskName = t.TaskName,
                    Status = t.Status.ToString(),
                    Progress = t.Progress,
                    CreatedAt = t.CreatedAt,
                    StartedAt = t.StartedAt,
                    EstimatedTimeRemaining = t.EstimatedTimeRemaining,
                    ConversionSpeed = t.ConversionSpeed
                }).ToList();

                await Clients.Caller.SendAsync("ActiveTasks", taskData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取活动任务失败");
                await Clients.Caller.SendAsync("Error", $"获取活动任务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 请求取消任务
        /// </summary>
        public async Task CancelTask(string taskId)
        {
            try
            {
                // 这里可以添加权限验证
                // 实际的取消逻辑在VideoConversionService中实现
                await Clients.All.SendAsync("CancelTaskRequested", taskId);
                _logger.LogInformation("收到取消任务请求: {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消任务请求失败: {TaskId}", taskId);
                await Clients.Caller.SendAsync("Error", $"取消任务失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// SignalR扩展方法
    /// </summary>
    public static class ConversionHubExtensions
    {
        /// <summary>
        /// 发送任务进度更新到特定任务组
        /// </summary>
        public static async Task SendTaskProgressAsync(this IHubContext<ConversionHub> hubContext, 
            string taskId, int progress, string message, double? speed = null, int? remainingSeconds = null)
        {
            await hubContext.Clients.Group($"task_{taskId}").SendAsync("ProgressUpdate", new
            {
                TaskId = taskId,
                Progress = progress,
                Message = message,
                Speed = speed,
                RemainingSeconds = remainingSeconds,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// 发送任务状态更新到特定任务组
        /// </summary>
        public static async Task SendTaskStatusAsync(this IHubContext<ConversionHub> hubContext, 
            string taskId, string status, string? errorMessage = null)
        {
            await hubContext.Clients.Group($"task_{taskId}").SendAsync("StatusUpdate", new
            {
                TaskId = taskId,
                Status = status,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// 发送任务完成通知到所有客户端
        /// </summary>
        public static async Task SendTaskCompletedAsync(this IHubContext<ConversionHub> hubContext, 
            string taskId, string taskName, bool success, string? errorMessage = null)
        {
            await hubContext.Clients.All.SendAsync("TaskCompleted", new
            {
                TaskId = taskId,
                TaskName = taskName,
                Success = success,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// 发送系统通知到所有客户端
        /// </summary>
        public static async Task SendSystemNotificationAsync(this IHubContext<ConversionHub> hubContext, 
            string message, string type = "info")
        {
            await hubContext.Clients.All.SendAsync("SystemNotification", new
            {
                Message = message,
                Type = type,
                Timestamp = DateTime.Now
            });
        }
    }
}
