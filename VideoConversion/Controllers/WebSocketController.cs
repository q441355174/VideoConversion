using Microsoft.AspNetCore.Mvc;
using VideoConversion.Controllers.Base;
using VideoConversion.Services;

namespace VideoConversion.Controllers
{
    /// <summary>
    /// WebSocket管理控制器
    /// </summary>
    [Route("api/[controller]")]
    public class WebSocketController : BaseApiController
    {
        private readonly IWebSocketService _webSocketService;
        private readonly WebSocketNotificationService _notificationService;
        private readonly WebSocketConnectionManager _connectionManager;

        public WebSocketController(
            IWebSocketService webSocketService,
            WebSocketNotificationService notificationService,
            WebSocketConnectionManager connectionManager,
            ILogger<WebSocketController> logger) : base(logger)
        {
            _webSocketService = webSocketService;
            _notificationService = notificationService;
            _connectionManager = connectionManager;
        }

        /// <summary>
        /// 获取WebSocket连接统计信息
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetConnectionStats()
        {
            return await SafeExecuteAsync(
                async () =>
                {
                    var stats = await _notificationService.GetConnectionStatsAsync();
                    var activeConnections = _connectionManager.GetActiveConnections().ToList();
                    
                    return new
                    {
                        totalConnections = _webSocketService.GetConnectionCount(),
                        activeConnections = activeConnections.Count,
                        connections = activeConnections.Select(c => new
                        {
                            connectionId = c.ConnectionId,
                            connectedAt = c.ConnectedAt,
                            lastPingAt = c.LastPingAt,
                            userId = c.UserId,
                            ipAddress = c.IpAddress,
                            userAgent = c.UserAgent,
                            groups = c.Groups.ToList(),
                            isAlive = c.IsAlive
                        }).ToList(),
                        serverTime = DateTime.Now
                    };
                },
                "获取WebSocket统计信息",
                "WebSocket统计信息获取成功"
            );
        }

        /// <summary>
        /// 发送系统通知给所有连接
        /// </summary>
        [HttpPost("broadcast")]
        public async Task<IActionResult> BroadcastMessage([FromBody] BroadcastMessageRequest request)
        {
            if (string.IsNullOrEmpty(request.Message))
                return ValidationError("消息内容不能为空");

            return await SafeExecuteAsync(
                async () =>
                {
                    await _notificationService.NotifySystemMessageAsync(
                        request.Title ?? "系统通知",
                        request.Message,
                        request.Level ?? "info"
                    );

                    return new
                    {
                        message = "系统通知已发送",
                        recipients = _webSocketService.GetConnectionCount(),
                        timestamp = DateTime.Now
                    };
                },
                "发送系统通知",
                "系统通知发送成功"
            );
        }

        /// <summary>
        /// 发送消息给指定连接
        /// </summary>
        [HttpPost("send/{connectionId}")]
        public async Task<IActionResult> SendMessage(string connectionId, [FromBody] SendMessageRequest request)
        {
            if (string.IsNullOrEmpty(connectionId))
                return ValidationError("连接ID不能为空");

            if (string.IsNullOrEmpty(request.Message))
                return ValidationError("消息内容不能为空");

            return await SafeExecuteAsync(
                async () =>
                {
                    var connection = _connectionManager.GetConnection(connectionId);
                    if (connection == null)
                    {
                        throw new ArgumentException("连接不存在或已断开");
                    }

                    await _notificationService.SendCustomMessageAsync(
                        connectionId,
                        request.Action ?? "message",
                        new { message = request.Message, timestamp = DateTime.Now }
                    );

                    return new
                    {
                        message = "消息已发送",
                        connectionId = connectionId,
                        timestamp = DateTime.Now
                    };
                },
                "发送消息",
                "消息发送成功"
            );
        }

        /// <summary>
        /// 断开指定连接
        /// </summary>
        [HttpPost("disconnect/{connectionId}")]
        public async Task<IActionResult> DisconnectConnection(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId))
                return ValidationError("连接ID不能为空");

            return await SafeExecuteAsync(
                async () =>
                {
                    var connection = _connectionManager.GetConnection(connectionId);
                    if (connection == null)
                    {
                        throw new ArgumentException("连接不存在");
                    }

                    await _webSocketService.DisconnectAsync(connectionId);

                    return new
                    {
                        message = "连接已断开",
                        connectionId = connectionId,
                        timestamp = DateTime.Now
                    };
                },
                "断开连接",
                "连接断开成功"
            );
        }

        /// <summary>
        /// 清理断开的连接
        /// </summary>
        [HttpPost("cleanup")]
        public async Task<IActionResult> CleanupConnections()
        {
            return await SafeExecuteAsync(
                async () =>
                {
                    var beforeCount = _webSocketService.GetConnectionCount();
                    await _webSocketService.CleanupDisconnectedConnectionsAsync();
                    var afterCount = _webSocketService.GetConnectionCount();

                    return new
                    {
                        message = "连接清理完成",
                        beforeCount = beforeCount,
                        afterCount = afterCount,
                        cleanedCount = beforeCount - afterCount,
                        timestamp = DateTime.Now
                    };
                },
                "清理连接",
                "连接清理完成"
            );
        }

        /// <summary>
        /// 获取指定组的连接信息
        /// </summary>
        [HttpGet("groups/{groupName}")]
        public async Task<IActionResult> GetGroupConnections(string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
                return ValidationError("组名不能为空");

            return await SafeExecuteAsync(
                async () =>
                {
                    var connections = _connectionManager.GetGroupConnections(groupName).ToList();
                    
                    return new
                    {
                        groupName = groupName,
                        connectionCount = connections.Count,
                        connections = connections.Select(c => new
                        {
                            connectionId = c.ConnectionId,
                            connectedAt = c.ConnectedAt,
                            userId = c.UserId,
                            ipAddress = c.IpAddress,
                            isAlive = c.IsAlive
                        }).ToList()
                    };
                },
                "获取组连接信息",
                "组连接信息获取成功"
            );
        }

        /// <summary>
        /// 发送消息给指定组
        /// </summary>
        [HttpPost("groups/{groupName}/send")]
        public async Task<IActionResult> SendToGroup(string groupName, [FromBody] SendMessageRequest request)
        {
            if (string.IsNullOrEmpty(groupName))
                return ValidationError("组名不能为空");

            if (string.IsNullOrEmpty(request.Message))
                return ValidationError("消息内容不能为空");

            return await SafeExecuteAsync(
                async () =>
                {
                    var connectionCount = _webSocketService.GetGroupConnectionCount(groupName);
                    if (connectionCount == 0)
                    {
                        throw new ArgumentException("组中没有活跃连接");
                    }

                    await _notificationService.BroadcastCustomMessageAsync(
                        request.Action ?? "group_message",
                        new 
                        { 
                            groupName = groupName,
                            message = request.Message, 
                            timestamp = DateTime.Now 
                        }
                    );

                    return new
                    {
                        message = "组消息已发送",
                        groupName = groupName,
                        recipients = connectionCount,
                        timestamp = DateTime.Now
                    };
                },
                "发送组消息",
                "组消息发送成功"
            );
        }
    }

    /// <summary>
    /// 广播消息请求模型
    /// </summary>
    public class BroadcastMessageRequest
    {
        public string? Title { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Level { get; set; } = "info";
    }

    /// <summary>
    /// 发送消息请求模型
    /// </summary>
    public class SendMessageRequest
    {
        public string Message { get; set; } = string.Empty;
        public string? Action { get; set; }
    }
}
