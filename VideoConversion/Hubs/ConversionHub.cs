using Microsoft.AspNetCore.SignalR;
using VideoConversion.Services;
using VideoConversion.Models;

namespace VideoConversion.Hubs
{
    /// <summary>
    /// 视频转换进度推送Hub
    /// </summary>
    public class ConversionHub : Hub
    {
        private readonly DatabaseService _databaseService;
        private readonly ILogger<ConversionHub> _logger;
        private readonly ConversionQueueService _queueService;
        public ConversionHub(
            DatabaseService databaseService,
            ILogger<ConversionHub> logger,
            ConversionQueueService queueService)
        {
            _databaseService = databaseService;
            _logger = logger;
            _queueService = queueService;
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
                    // 使用状态映射服务返回统一格式的数据
                    var taskInfo = StatusMappingService.CreateDetailedTaskInfo(task);
                    await Clients.Caller.SendAsync("TaskStatus", taskInfo);
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
                // 使用状态映射服务返回统一格式的数据
                var taskData = tasks.Select(StatusMappingService.CreateSimpleTaskInfo).ToList();

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
                _logger.LogInformation("收到取消任务请求: {TaskId} from {ConnectionId}", taskId, Context.ConnectionId);

                // 验证任务是否存在
                var task = await _databaseService.GetTaskAsync(taskId);
                if (task == null)
                {
                    await Clients.Caller.SendAsync("TaskNotFound", taskId);
                    return;
                }

                // 检查任务状态是否可以取消
                if (task.Status != ConversionStatus.Pending && task.Status != ConversionStatus.Converting)
                {
                    await Clients.Caller.SendAsync("Error", $"任务状态为 {task.Status}，无法取消");
                    return;
                }

                // 调用队列服务取消任务
                await _queueService.CancelTaskAsync(taskId);

                // 通知所有客户端任务已取消
                await Clients.All.SendAsync("TaskCancelled", new
                {
                    TaskId = taskId,
                    Message = "任务已取消",
                    Timestamp = DateTime.Now
                });

                // 发送确认消息给请求者
                await Clients.Caller.SendAsync("TaskCancelCompleted", new
                {
                    TaskId = taskId,
                    Message = "任务取消成功",
                    Timestamp = DateTime.Now
                });

                _logger.LogInformation("✅ 任务取消完成: {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 取消任务请求失败: {TaskId}", taskId);
                await Clients.Caller.SendAsync("Error", $"取消任务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取最近任务列表
        /// </summary>
        public async Task<object> GetRecentTasks(int count = 10)
        {
            try
            {
                _logger.LogInformation("获取最近任务列表，数量: {Count}", count);

                // 尝试从数据库获取最近任务
                var tasks = await _databaseService.GetRecentTasksAsync(count);

                if (tasks != null && tasks.Any())
                {
                    // 使用状态映射服务返回统一格式的数据
                    var taskData = tasks.Select(StatusMappingService.CreateSimpleTaskInfo).ToList();

                    return new
                    {
                        success = true,
                        data = taskData
                    };
                }
                else
                {
                    // 如果数据库中没有数据，返回模拟数据
                    return GetMockRecentTasks(count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取最近任务失败");

                // 发生错误时返回模拟数据
                return GetMockRecentTasks(count);
            }
        }

        /// <summary>
        /// 获取模拟的最近任务数据（用于演示）
        /// </summary>
        private object GetMockRecentTasks(int count)
        {
            var mockTasks = new[]
            {
                new
                {
                    taskId = "task-001",
                    fileName = "sample_video.mp4",
                    status = "Completed",
                    progress = 100,
                    createdAt = DateTime.Now.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    downloadUrl = "/downloads/sample_video_converted.mp4"
                },
                new
                {
                    taskId = "task-002",
                    fileName = "another_video.avi",
                    status = "Failed",
                    progress = 45,
                    createdAt = DateTime.Now.AddHours(-2).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    downloadUrl = (string?)null
                },
                new
                {
                    taskId = "task-003",
                    fileName = "test_video.mkv",
                    status = "Running",
                    progress = 75,
                    createdAt = DateTime.Now.AddMinutes(-30).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    downloadUrl = (string?)null
                },
                new
                {
                    taskId = "task-004",
                    fileName = "demo_video.mp4",
                    status = "Pending",
                    progress = 0,
                    createdAt = DateTime.Now.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    downloadUrl = (string?)null
                },
                new
                {
                    taskId = "task-005",
                    fileName = "large_video.mov",
                    status = "Cancelled",
                    progress = 25,
                    createdAt = DateTime.Now.AddHours(-3).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    downloadUrl = (string?)null
                }
            };

            return new
            {
                success = true,
                data = mockTasks.Take(count).ToArray()
            };
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
