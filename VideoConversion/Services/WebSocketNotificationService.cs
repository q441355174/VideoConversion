using System.Text.Json;
using VideoConversion.Models;

namespace VideoConversion.Services
{
    /// <summary>
    /// WebSocket通知服务 - 与业务逻辑集成
    /// </summary>
    public class WebSocketNotificationService
    {
        private readonly IWebSocketService _webSocketService;
        private readonly ILogger<WebSocketNotificationService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public WebSocketNotificationService(
            IWebSocketService webSocketService,
            ILogger<WebSocketNotificationService> logger)
        {
            _webSocketService = webSocketService;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false 
            };
        }

        /// <summary>
        /// 发送任务状态更新通知
        /// </summary>
        public async Task NotifyTaskStatusUpdateAsync(string taskId, ConversionStatus status, int progress = 0, string? message = null)
        {
            try
            {
                var notification = new TaskStatusUpdateMessage
                {
                    TaskId = taskId,
                    Status = status.ToString(),
                    Progress = progress,
                    Message = message
                };

                var json = JsonSerializer.Serialize(notification, _jsonOptions);
                
                // 发送给任务组
                await _webSocketService.SendToGroupAsync($"task_{taskId}", json);
                
                // 也发送给所有连接（用于任务列表更新）
                await _webSocketService.BroadcastMessageAsync(json);

                _logger.LogDebug("已发送任务状态更新通知: {TaskId} - {Status}", taskId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送任务状态更新通知失败: {TaskId}", taskId);
            }
        }

        /// <summary>
        /// 发送任务进度更新通知
        /// </summary>
        public async Task NotifyTaskProgressUpdateAsync(string taskId, int progress, string? currentTime = null, 
            string? estimatedTimeRemaining = null, string? conversionSpeed = null)
        {
            try
            {
                var notification = new TaskProgressUpdateMessage
                {
                    TaskId = taskId,
                    Progress = progress,
                    CurrentTime = currentTime,
                    EstimatedTimeRemaining = estimatedTimeRemaining,
                    ConversionSpeed = conversionSpeed
                };

                var json = JsonSerializer.Serialize(notification, _jsonOptions);
                
                // 只发送给关注此任务的连接
                await _webSocketService.SendToGroupAsync($"task_{taskId}", json);

                _logger.LogDebug("已发送任务进度更新通知: {TaskId} - {Progress}%", taskId, progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送任务进度更新通知失败: {TaskId}", taskId);
            }
        }

        /// <summary>
        /// 发送任务完成通知
        /// </summary>
        public async Task NotifyTaskCompletedAsync(string taskId, string? taskName = null, string? fileName = null, 
            string? outputPath = null, TimeSpan? duration = null, long? fileSize = null)
        {
            try
            {
                var notification = new TaskCompletedMessage
                {
                    TaskId = taskId,
                    TaskName = taskName,
                    FileName = fileName,
                    OutputPath = outputPath,
                    Duration = duration,
                    FileSize = fileSize
                };

                var json = JsonSerializer.Serialize(notification, _jsonOptions);
                
                // 发送给任务组和所有连接
                await _webSocketService.SendToGroupAsync($"task_{taskId}", json);
                await _webSocketService.BroadcastMessageAsync(json);

                _logger.LogInformation("已发送任务完成通知: {TaskId} - {TaskName}", taskId, taskName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送任务完成通知失败: {TaskId}", taskId);
            }
        }

        /// <summary>
        /// 发送任务失败通知
        /// </summary>
        public async Task NotifyTaskFailedAsync(string taskId, string? taskName = null, string? fileName = null, string? errorMessage = null)
        {
            try
            {
                var notification = new TaskFailedMessage
                {
                    TaskId = taskId,
                    TaskName = taskName,
                    FileName = fileName,
                    ErrorMessage = errorMessage
                };

                var json = JsonSerializer.Serialize(notification, _jsonOptions);
                
                // 发送给任务组和所有连接
                await _webSocketService.SendToGroupAsync($"task_{taskId}", json);
                await _webSocketService.BroadcastMessageAsync(json);

                _logger.LogWarning("已发送任务失败通知: {TaskId} - {ErrorMessage}", taskId, errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送任务失败通知失败: {TaskId}", taskId);
            }
        }

        /// <summary>
        /// 发送系统通知
        /// </summary>
        public async Task NotifySystemMessageAsync(string title, string message, string level = "info")
        {
            try
            {
                var notification = new SystemNotificationMessage
                {
                    Title = title,
                    Message = message,
                    Level = level
                };

                var json = JsonSerializer.Serialize(notification, _jsonOptions);
                await _webSocketService.BroadcastMessageAsync(json);

                _logger.LogInformation("已发送系统通知: {Title} - {Message}", title, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送系统通知失败: {Title}", title);
            }
        }

        /// <summary>
        /// 让连接加入任务组（用于接收特定任务的更新）
        /// </summary>
        public async Task JoinTaskGroupAsync(string connectionId, string taskId)
        {
            try
            {
                await _webSocketService.AddToGroupAsync(connectionId, $"task_{taskId}");
                _logger.LogDebug("连接 {ConnectionId} 已加入任务组 {TaskId}", connectionId, taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加入任务组失败: {ConnectionId} - {TaskId}", connectionId, taskId);
            }
        }

        /// <summary>
        /// 让连接离开任务组
        /// </summary>
        public async Task LeaveTaskGroupAsync(string connectionId, string taskId)
        {
            try
            {
                await _webSocketService.RemoveFromGroupAsync(connectionId, $"task_{taskId}");
                _logger.LogDebug("连接 {ConnectionId} 已离开任务组 {TaskId}", connectionId, taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "离开任务组失败: {ConnectionId} - {TaskId}", connectionId, taskId);
            }
        }

        /// <summary>
        /// 获取连接统计信息
        /// </summary>
        public Task<object> GetConnectionStatsAsync()
        {
            try
            {
                var result = new
                {
                    totalConnections = _webSocketService.GetConnectionCount(),
                    timestamp = DateTime.Now
                };
                return Task.FromResult<object>(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取连接统计信息失败");
                return Task.FromResult<object>(new { error = "获取统计信息失败" });
            }
        }

        /// <summary>
        /// 发送自定义消息给指定连接
        /// </summary>
        public async Task SendCustomMessageAsync(string connectionId, string action, object? payload = null)
        {
            try
            {
                var message = new CustomMessage
                {
                    ConnectionId = connectionId,
                    Action = action,
                    Payload = payload
                };

                var json = JsonSerializer.Serialize(message, _jsonOptions);
                await _webSocketService.SendMessageAsync(connectionId, json);

                _logger.LogDebug("已发送自定义消息: {ConnectionId} - {Action}", connectionId, action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送自定义消息失败: {ConnectionId} - {Action}", connectionId, action);
            }
        }

        /// <summary>
        /// 广播自定义消息
        /// </summary>
        public async Task BroadcastCustomMessageAsync(string action, object? payload = null)
        {
            try
            {
                var message = new CustomMessage
                {
                    Action = action,
                    Payload = payload
                };

                var json = JsonSerializer.Serialize(message, _jsonOptions);
                await _webSocketService.BroadcastMessageAsync(json);

                _logger.LogDebug("已广播自定义消息: {Action}", action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "广播自定义消息失败: {Action}", action);
            }
        }
    }
}
