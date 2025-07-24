using System.Net.WebSockets;
using VideoConversion.Services;

namespace VideoConversion.Middleware
{
    /// <summary>
    /// WebSocket中间件
    /// </summary>
    public class WebSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<WebSocketMiddleware> _logger;
        private readonly WebSocketConnectionManager _connectionManager;

        public WebSocketMiddleware(
            RequestDelegate next,
            ILogger<WebSocketMiddleware> logger,
            WebSocketConnectionManager connectionManager)
        {
            _next = next;
            _logger = logger;
            _connectionManager = connectionManager;
        } 

        public async Task InvokeAsync(HttpContext context)
        {
            // 检查是否是WebSocket请求
            if (context.Request.Path == "/ws" && context.WebSockets.IsWebSocketRequest)
            {
                await HandleWebSocketRequestAsync(context);
            }
            else
            {
                await _next(context);
            }
        }

        /// <summary>
        /// 处理WebSocket请求
        /// </summary>
        private async Task HandleWebSocketRequestAsync(HttpContext context)
        {
            try
            {
                // 接受WebSocket连接
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                // 获取连接信息
                var userId = context.User?.Identity?.Name;
                var userAgent = context.Request.Headers["User-Agent"].ToString();
                var ipAddress = GetClientIpAddress(context);

                // 添加连接到管理器
                var connectionId = _connectionManager.AddConnection(webSocket, userId, userAgent, ipAddress);

                _logger.LogInformation("WebSocket连接已建立: {ConnectionId}, IP: {IpAddress}, UserAgent: {UserAgent}",
                    connectionId, ipAddress, userAgent);

                // 获取WebSocket服务并处理通信
                var webSocketService = context.RequestServices.GetRequiredService<IWebSocketService>();
                await webSocketService.HandleWebSocketAsync(webSocket, connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理WebSocket请求时发生错误");
                context.Response.StatusCode = 500;
            }
        }

        /// <summary>
        /// 获取客户端IP地址
        /// </summary>
        private string GetClientIpAddress(HttpContext context)
        {
            // 检查X-Forwarded-For头（代理服务器）
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            // 检查X-Real-IP头（Nginx代理）
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            // 使用RemoteIpAddress
            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }
    }

    /// <summary>
    /// WebSocket中间件扩展方法
    /// </summary>
    public static class WebSocketMiddlewareExtensions
    {
        /// <summary>
        /// 添加WebSocket中间件
        /// </summary>
        public static IApplicationBuilder UseWebSocketMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<WebSocketMiddleware>();
        }

        /// <summary>
        /// 添加WebSocket服务
        /// </summary>
        public static IServiceCollection AddWebSocketServices(this IServiceCollection services)
        {
            services.AddSingleton<WebSocketConnectionManager>();
            services.AddScoped<IWebSocketService, WebSocketService>();

            return services;
        }
    }
}
