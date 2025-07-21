using System.Net.WebSockets;

namespace VideoConversion.Services
{
    /// <summary>
    /// WebSocket服务接口
    /// </summary>
    public interface IWebSocketService
    {
        /// <summary>
        /// 处理WebSocket连接
        /// </summary>
        Task HandleWebSocketAsync(WebSocket webSocket, string connectionId);

        /// <summary>
        /// 发送消息给指定连接
        /// </summary>
        Task SendMessageAsync(string connectionId, string message);

        /// <summary>
        /// 发送消息给所有连接
        /// </summary>
        Task BroadcastMessageAsync(string message);

        /// <summary>
        /// 发送消息给指定组
        /// </summary>
        Task SendToGroupAsync(string groupName, string message);

        /// <summary>
        /// 将连接加入组
        /// </summary>
        Task AddToGroupAsync(string connectionId, string groupName);

        /// <summary>
        /// 将连接从组中移除
        /// </summary>
        Task RemoveFromGroupAsync(string connectionId, string groupName);

        /// <summary>
        /// 获取连接数量
        /// </summary>
        int GetConnectionCount();

        /// <summary>
        /// 获取组中的连接数量
        /// </summary>
        int GetGroupConnectionCount(string groupName);

        /// <summary>
        /// 断开指定连接
        /// </summary>
        Task DisconnectAsync(string connectionId);

        /// <summary>
        /// 清理断开的连接
        /// </summary>
        Task CleanupDisconnectedConnectionsAsync();
    }
}
