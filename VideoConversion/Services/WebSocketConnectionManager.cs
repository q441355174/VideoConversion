using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace VideoConversion.Services
{
    /// <summary>
    /// WebSocket连接信息
    /// </summary>
    public class WebSocketConnection
    {
        public string ConnectionId { get; set; } = string.Empty;
        public WebSocket WebSocket { get; set; } = null!;
        public DateTime ConnectedAt { get; set; }
        public string? UserId { get; set; }
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
        public HashSet<string> Groups { get; set; } = new();
        public DateTime LastPingAt { get; set; }
        public bool IsAlive => WebSocket.State == WebSocketState.Open; 
    }

    /// <summary>
    /// WebSocket连接管理器
    /// </summary>
    public class WebSocketConnectionManager
    {
        private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _groups = new();
        private readonly ILogger<WebSocketConnectionManager> _logger;
        private readonly Timer _cleanupTimer;

        public WebSocketConnectionManager(ILogger<WebSocketConnectionManager> logger)
        {
            _logger = logger;
            
            // 每30秒清理一次断开的连接
            _cleanupTimer = new Timer(CleanupDisconnectedConnections, null, 
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// 添加连接
        /// </summary>
        public string AddConnection(WebSocket webSocket, string? userId = null, 
            string? userAgent = null, string? ipAddress = null)
        {
            var connectionId = Guid.NewGuid().ToString();
            var connection = new WebSocketConnection
            {
                ConnectionId = connectionId,
                WebSocket = webSocket,
                ConnectedAt = DateTime.Now,
                LastPingAt = DateTime.Now,
                UserId = userId,
                UserAgent = userAgent,
                IpAddress = ipAddress
            };

            _connections.TryAdd(connectionId, connection);
            _logger.LogInformation("WebSocket连接已添加: {ConnectionId}, 总连接数: {Count}", 
                connectionId, _connections.Count);

            return connectionId;
        }

        /// <summary>
        /// 移除连接
        /// </summary>
        public async Task RemoveConnectionAsync(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var connection))
            {
                // 从所有组中移除
                foreach (var groupName in connection.Groups.ToList())
                {
                    await RemoveFromGroupAsync(connectionId, groupName);
                }

                // 关闭WebSocket连接
                if (connection.WebSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await connection.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                            "Connection removed", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "关闭WebSocket连接时出错: {ConnectionId}", connectionId);
                    }
                }

                _logger.LogInformation("WebSocket连接已移除: {ConnectionId}, 剩余连接数: {Count}", 
                    connectionId, _connections.Count);
            }
        }

        /// <summary>
        /// 获取连接
        /// </summary>
        public WebSocketConnection? GetConnection(string connectionId)
        {
            _connections.TryGetValue(connectionId, out var connection);
            return connection;
        }

        /// <summary>
        /// 获取所有活跃连接
        /// </summary>
        public IEnumerable<WebSocketConnection> GetActiveConnections()
        {
            return _connections.Values.Where(c => c.IsAlive);
        }

        /// <summary>
        /// 加入组
        /// </summary>
        public Task AddToGroupAsync(string connectionId, string groupName)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.Groups.Add(groupName);
                
                _groups.AddOrUpdate(groupName, 
                    new HashSet<string> { connectionId },
                    (key, existing) => 
                    {
                        existing.Add(connectionId);
                        return existing;
                    });

                _logger.LogDebug("连接 {ConnectionId} 已加入组 {GroupName}", connectionId, groupName);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 离开组
        /// </summary>
        public Task RemoveFromGroupAsync(string connectionId, string groupName)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.Groups.Remove(groupName);
            }

            if (_groups.TryGetValue(groupName, out var group))
            {
                group.Remove(connectionId);
                if (group.Count == 0)
                {
                    _groups.TryRemove(groupName, out _);
                }
            }

            _logger.LogDebug("连接 {ConnectionId} 已离开组 {GroupName}", connectionId, groupName);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 获取组中的连接
        /// </summary>
        public IEnumerable<WebSocketConnection> GetGroupConnections(string groupName)
        {
            if (_groups.TryGetValue(groupName, out var connectionIds))
            {
                return connectionIds
                    .Select(id => GetConnection(id))
                    .Where(c => c != null && c.IsAlive)
                    .Cast<WebSocketConnection>();
            }

            return Enumerable.Empty<WebSocketConnection>();
        }

        /// <summary>
        /// 获取连接数量
        /// </summary>
        public int GetConnectionCount() => _connections.Count;

        /// <summary>
        /// 获取活跃连接数量
        /// </summary>
        public int GetActiveConnectionCount() => _connections.Values.Count(c => c.IsAlive);

        /// <summary>
        /// 获取组连接数量
        /// </summary>
        public int GetGroupConnectionCount(string groupName)
        {
            return GetGroupConnections(groupName).Count();
        }

        /// <summary>
        /// 更新连接的最后ping时间
        /// </summary>
        public void UpdateLastPing(string connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.LastPingAt = DateTime.Now;
            }
        }

        /// <summary>
        /// 清理断开的连接
        /// </summary>
        private async void CleanupDisconnectedConnections(object? state)
        {
            var disconnectedConnections = _connections.Values
                .Where(c => !c.IsAlive || DateTime.Now - c.LastPingAt > TimeSpan.FromMinutes(5))
                .ToList();

            foreach (var connection in disconnectedConnections)
            {
                await RemoveConnectionAsync(connection.ConnectionId);
            }

            if (disconnectedConnections.Any())
            {
                _logger.LogInformation("清理了 {Count} 个断开的WebSocket连接", disconnectedConnections.Count);
            }
        }

        /// <summary>
        /// 手动清理断开的连接
        /// </summary>
        public async Task CleanupDisconnectedConnectionsAsync()
        {
            CleanupDisconnectedConnections(null);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            
            // 关闭所有连接
            var tasks = _connections.Values.Select(async connection =>
            {
                if (connection.WebSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await connection.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                            "Server shutdown", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "关闭WebSocket连接时出错: {ConnectionId}", connection.ConnectionId);
                    }
                }
            });

            Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(5));
        }
    }
}
