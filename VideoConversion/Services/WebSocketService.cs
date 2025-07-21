using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using VideoConversion.Models;

namespace VideoConversion.Services
{
    /// <summary>
    /// WebSocket服务实现
    /// </summary>
    public class WebSocketService : IWebSocketService
    {
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly ILogger<WebSocketService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public WebSocketService(
            WebSocketConnectionManager connectionManager,
            ILogger<WebSocketService> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        /// <summary>
        /// 处理WebSocket连接
        /// </summary>
        public async Task HandleWebSocketAsync(WebSocket webSocket, string connectionId)
        {
            _logger.LogInformation("开始处理WebSocket连接: {ConnectionId}", connectionId);

            try
            {
                // 发送连接确认消息
                await SendConnectionInfoAsync(connectionId);

                // 监听消息
                await ListenForMessagesAsync(webSocket, connectionId);
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "WebSocket连接异常: {ConnectionId}", connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理WebSocket连接时发生错误: {ConnectionId}", connectionId);
            }
            finally
            {
                await _connectionManager.RemoveConnectionAsync(connectionId);
                _logger.LogInformation("WebSocket连接已关闭: {ConnectionId}", connectionId);
            }
        }

        /// <summary>
        /// 监听WebSocket消息
        /// </summary>
        private async Task ListenForMessagesAsync(WebSocket webSocket, string connectionId)
        {
            var buffer = new byte[4096];

            while (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessMessageAsync(connectionId, message);
                    }
                    else if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("收到WebSocket关闭消息: {ConnectionId}", connectionId);
                        break;
                    }

                    // 更新最后活跃时间
                    _connectionManager.UpdateLastPing(connectionId);
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    _logger.LogInformation("WebSocket连接意外关闭: {ConnectionId}", connectionId);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理WebSocket消息时发生错误: {ConnectionId}", connectionId);
                    break;
                }
            }
        }

        /// <summary>
        /// 处理收到的消息
        /// </summary>
        private async Task ProcessMessageAsync(string connectionId, string message)
        {
            try
            {
                _logger.LogDebug("收到WebSocket消息: {ConnectionId} - {Message}", connectionId, message);

                var webSocketMessage = JsonSerializer.Deserialize<WebSocketMessage>(message, _jsonOptions);
                if (webSocketMessage == null)
                {
                    await SendErrorAsync(connectionId, "无效的消息格式");
                    return;
                }

                switch (webSocketMessage.Type)
                {
                    case Models.WebSocketMessageType.Ping:
                        await HandlePingAsync(connectionId);
                        break;

                    case Models.WebSocketMessageType.JoinGroup:
                        await HandleJoinGroupAsync(connectionId, message);
                        break;

                    case Models.WebSocketMessageType.LeaveGroup:
                        await HandleLeaveGroupAsync(connectionId, message);
                        break;

                    case Models.WebSocketMessageType.CustomMessage:
                        await HandleCustomMessageAsync(connectionId, message);
                        break;

                    default:
                        _logger.LogWarning("未处理的消息类型: {MessageType}", webSocketMessage.Type);
                        break;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "解析WebSocket消息失败: {ConnectionId}", connectionId);
                await SendErrorAsync(connectionId, "消息格式错误");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理WebSocket消息时发生错误: {ConnectionId}", connectionId);
                await SendErrorAsync(connectionId, "处理消息时发生内部错误");
            }
        }

        /// <summary>
        /// 处理Ping消息
        /// </summary>
        private async Task HandlePingAsync(string connectionId)
        {
            var pongMessage = new PongMessage();
            await SendMessageAsync(connectionId, JsonSerializer.Serialize(pongMessage, _jsonOptions));
        }

        /// <summary>
        /// 处理加入组消息
        /// </summary>
        private async Task HandleJoinGroupAsync(string connectionId, string message)
        {
            var groupMessage = JsonSerializer.Deserialize<GroupMessage>(message, _jsonOptions);
            if (groupMessage != null && !string.IsNullOrEmpty(groupMessage.GroupName))
            {
                await AddToGroupAsync(connectionId, groupMessage.GroupName);
                
                var response = new WebSocketMessage
                {
                    Type = Models.WebSocketMessageType.JoinGroup,
                    Data = new { groupName = groupMessage.GroupName, success = true }
                };
                
                await SendMessageAsync(connectionId, JsonSerializer.Serialize(response, _jsonOptions));
            }
        }

        /// <summary>
        /// 处理离开组消息
        /// </summary>
        private async Task HandleLeaveGroupAsync(string connectionId, string message)
        {
            var groupMessage = JsonSerializer.Deserialize<GroupMessage>(message, _jsonOptions);
            if (groupMessage != null && !string.IsNullOrEmpty(groupMessage.GroupName))
            {
                await RemoveFromGroupAsync(connectionId, groupMessage.GroupName);
                
                var response = new WebSocketMessage
                {
                    Type = Models.WebSocketMessageType.LeaveGroup,
                    Data = new { groupName = groupMessage.GroupName, success = true }
                };
                
                await SendMessageAsync(connectionId, JsonSerializer.Serialize(response, _jsonOptions));
            }
        }

        /// <summary>
        /// 处理自定义消息
        /// </summary>
        private async Task HandleCustomMessageAsync(string connectionId, string message)
        {
            var customMessage = JsonSerializer.Deserialize<CustomMessage>(message, _jsonOptions);
            if (customMessage != null)
            {
                _logger.LogInformation("收到自定义消息: {ConnectionId} - {Action}", connectionId, customMessage.Action);
                
                // 这里可以根据Action执行不同的业务逻辑
                // 例如：获取任务状态、取消任务等
                
                var response = new WebSocketMessage
                {
                    Type = Models.WebSocketMessageType.CustomMessage,
                    Data = new { action = customMessage.Action, processed = true }
                };
                
                await SendMessageAsync(connectionId, JsonSerializer.Serialize(response, _jsonOptions));
            }
        }

        /// <summary>
        /// 发送连接信息
        /// </summary>
        private async Task SendConnectionInfoAsync(string connectionId)
        {
            var connectionInfo = new ConnectionInfoMessage
            {
                ConnectionId = connectionId,
                TotalConnections = _connectionManager.GetActiveConnectionCount(),
                ServerInfo = new
                {
                    serverTime = DateTime.Now,
                    version = "1.0.0"
                }
            };

            await SendMessageAsync(connectionId, JsonSerializer.Serialize(connectionInfo, _jsonOptions));
        }

        /// <summary>
        /// 发送错误消息
        /// </summary>
        private async Task SendErrorAsync(string connectionId, string errorMessage)
        {
            var errorMsg = new WebSocketMessage
            {
                Type = Models.WebSocketMessageType.Error,
                Success = false,
                Error = errorMessage
            };

            await SendMessageAsync(connectionId, JsonSerializer.Serialize(errorMsg, _jsonOptions));
        }

        /// <summary>
        /// 发送消息给指定连接
        /// </summary>
        public async Task SendMessageAsync(string connectionId, string message)
        {
            var connection = _connectionManager.GetConnection(connectionId);
            if (connection?.IsAlive == true)
            {
                try
                {
                    var buffer = Encoding.UTF8.GetBytes(message);
                    await connection.WebSocket.SendAsync(
                        new ArraySegment<byte>(buffer),
                        System.Net.WebSockets.WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "发送WebSocket消息失败: {ConnectionId}", connectionId);
                    await _connectionManager.RemoveConnectionAsync(connectionId);
                }
            }
        }

        /// <summary>
        /// 广播消息给所有连接
        /// </summary>
        public async Task BroadcastMessageAsync(string message)
        {
            var connections = _connectionManager.GetActiveConnections().ToList();
            var tasks = connections.Select(conn => SendMessageAsync(conn.ConnectionId, message));
            
            await Task.WhenAll(tasks);
            _logger.LogDebug("广播消息给 {Count} 个连接", connections.Count);
        }

        /// <summary>
        /// 发送消息给指定组
        /// </summary>
        public async Task SendToGroupAsync(string groupName, string message)
        {
            var connections = _connectionManager.GetGroupConnections(groupName).ToList();
            var tasks = connections.Select(conn => SendMessageAsync(conn.ConnectionId, message));
            
            await Task.WhenAll(tasks);
            _logger.LogDebug("发送消息给组 {GroupName} 的 {Count} 个连接", groupName, connections.Count);
        }

        /// <summary>
        /// 将连接加入组
        /// </summary>
        public async Task AddToGroupAsync(string connectionId, string groupName)
        {
            await _connectionManager.AddToGroupAsync(connectionId, groupName);
        }

        /// <summary>
        /// 将连接从组中移除
        /// </summary>
        public async Task RemoveFromGroupAsync(string connectionId, string groupName)
        {
            await _connectionManager.RemoveFromGroupAsync(connectionId, groupName);
        }

        /// <summary>
        /// 获取连接数量
        /// </summary>
        public int GetConnectionCount()
        {
            return _connectionManager.GetActiveConnectionCount();
        }

        /// <summary>
        /// 获取组中的连接数量
        /// </summary>
        public int GetGroupConnectionCount(string groupName)
        {
            return _connectionManager.GetGroupConnectionCount(groupName);
        }

        /// <summary>
        /// 断开指定连接
        /// </summary>
        public async Task DisconnectAsync(string connectionId)
        {
            await _connectionManager.RemoveConnectionAsync(connectionId);
        }

        /// <summary>
        /// 清理断开的连接
        /// </summary>
        public async Task CleanupDisconnectedConnectionsAsync()
        {
            await _connectionManager.CleanupDisconnectedConnectionsAsync();
        }
    }
}
