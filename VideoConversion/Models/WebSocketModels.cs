using System.Text.Json.Serialization;

namespace VideoConversion.Models
{
    /// <summary>
    /// WebSocket消息类型
    /// </summary>
    public enum WebSocketMessageType
    {
        // 连接管理
        Connect,
        Disconnect,
        Ping,
        Pong,
        
        // 任务相关
        TaskStatusUpdate,
        TaskProgressUpdate,
        TaskCompleted,
        TaskFailed,
        TaskCancelled,
        
        // 系统通知
        SystemNotification,
        Error,
        
        // 组管理
        JoinGroup,
        LeaveGroup,
        
        // 自定义消息
        CustomMessage
    }

    /// <summary>
    /// WebSocket消息基类
    /// </summary>
    public class WebSocketMessage
    {
        [JsonPropertyName("type")]
        public WebSocketMessageType Type { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [JsonPropertyName("connectionId")]
        public string? ConnectionId { get; set; }

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;
    }

    /// <summary>
    /// 任务状态更新消息
    /// </summary>
    public class TaskStatusUpdateMessage : WebSocketMessage
    {
        public TaskStatusUpdateMessage()
        {
            Type = WebSocketMessageType.TaskStatusUpdate;
        }

        [JsonPropertyName("taskId")]
        public string TaskId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("progress")]
        public int Progress { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    /// <summary>
    /// 任务进度更新消息
    /// </summary>
    public class TaskProgressUpdateMessage : WebSocketMessage
    {
        public TaskProgressUpdateMessage()
        {
            Type = WebSocketMessageType.TaskProgressUpdate;
        }

        [JsonPropertyName("taskId")]
        public string TaskId { get; set; } = string.Empty;

        [JsonPropertyName("progress")]
        public int Progress { get; set; }

        [JsonPropertyName("currentTime")]
        public string? CurrentTime { get; set; }

        [JsonPropertyName("estimatedTimeRemaining")]
        public string? EstimatedTimeRemaining { get; set; }

        [JsonPropertyName("conversionSpeed")]
        public string? ConversionSpeed { get; set; }
    }

    /// <summary>
    /// 任务完成消息
    /// </summary>
    public class TaskCompletedMessage : WebSocketMessage
    {
        public TaskCompletedMessage()
        {
            Type = WebSocketMessageType.TaskCompleted;
        }

        [JsonPropertyName("taskId")]
        public string TaskId { get; set; } = string.Empty;

        [JsonPropertyName("taskName")]
        public string? TaskName { get; set; }

        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }

        [JsonPropertyName("outputPath")]
        public string? OutputPath { get; set; }

        [JsonPropertyName("duration")]
        public TimeSpan? Duration { get; set; }

        [JsonPropertyName("fileSize")]
        public long? FileSize { get; set; }
    }

    /// <summary>
    /// 任务失败消息
    /// </summary>
    public class TaskFailedMessage : WebSocketMessage
    {
        public TaskFailedMessage()
        {
            Type = WebSocketMessageType.TaskFailed;
            Success = false;
        }

        [JsonPropertyName("taskId")]
        public string TaskId { get; set; } = string.Empty;

        [JsonPropertyName("taskName")]
        public string? TaskName { get; set; }

        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 系统通知消息
    /// </summary>
    public class SystemNotificationMessage : WebSocketMessage
    {
        public SystemNotificationMessage()
        {
            Type = WebSocketMessageType.SystemNotification;
        }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("level")]
        public string Level { get; set; } = "info"; // info, warning, error, success
    }

    /// <summary>
    /// 组管理消息
    /// </summary>
    public class GroupMessage : WebSocketMessage
    {
        [JsonPropertyName("groupName")]
        public string GroupName { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty; // join, leave
    }

    /// <summary>
    /// Ping消息
    /// </summary>
    public class PingMessage : WebSocketMessage
    {
        public PingMessage()
        {
            Type = WebSocketMessageType.Ping;
        }
    }

    /// <summary>
    /// Pong消息
    /// </summary>
    public class PongMessage : WebSocketMessage
    {
        public PongMessage()
        {
            Type = WebSocketMessageType.Pong;
        }

        [JsonPropertyName("serverTime")]
        public DateTime ServerTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 连接信息消息
    /// </summary>
    public class ConnectionInfoMessage : WebSocketMessage
    {
        public ConnectionInfoMessage()
        {
            Type = WebSocketMessageType.Connect;
        }

        [JsonPropertyName("totalConnections")]
        public int TotalConnections { get; set; }

        [JsonPropertyName("serverInfo")]
        public object? ServerInfo { get; set; }
    }

    /// <summary>
    /// 自定义消息
    /// </summary>
    public class CustomMessage : WebSocketMessage
    {
        public CustomMessage()
        {
            Type = WebSocketMessageType.CustomMessage;
        }

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("payload")]
        public object? Payload { get; set; }
    }
}
