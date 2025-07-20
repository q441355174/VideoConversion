using System.Net;
using System.Text.Json;
using VideoConversion.Models;

namespace VideoConversion.Middleware
{
    /// <summary>
    /// 全局异常处理中间件
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // 记录异常日志
            _logger.LogError(exception, "未处理的异常发生在 {RequestPath}", context.Request.Path);

            // 设置响应内容类型
            context.Response.ContentType = "application/json";

            // 根据异常类型设置响应
            var response = CreateErrorResponse(exception);
            context.Response.StatusCode = response.StatusCode;

            // 序列化响应
            var jsonResponse = JsonSerializer.Serialize(response.ApiResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await context.Response.WriteAsync(jsonResponse);
        }

        private ErrorResponse CreateErrorResponse(Exception exception)
        {
            return exception switch
            {
                ArgumentNullException nullEx => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    ApiResponse = ApiResponse.CreateError(
                        $"必需参数不能为空: {nullEx.ParamName}",
                        "NULL_ARGUMENT"
                    )
                },

                ArgumentException argEx => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    ApiResponse = ApiResponse.CreateError(
                        argEx.Message,
                        "INVALID_ARGUMENT"
                    )
                },

                FileNotFoundException fileEx => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.NotFound,
                    ApiResponse = ApiResponse.CreateError(
                        fileEx.Message,
                        "FILE_NOT_FOUND"
                    )
                },

                DirectoryNotFoundException dirEx => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.NotFound,
                    ApiResponse = ApiResponse.CreateError(
                        dirEx.Message,
                        "DIRECTORY_NOT_FOUND"
                    )
                },

                UnauthorizedAccessException => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.Forbidden,
                    ApiResponse = ApiResponse.CreateError(
                        "访问被拒绝",
                        "ACCESS_DENIED"
                    )
                },

                InvalidOperationException invalidEx => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.Conflict,
                    ApiResponse = ApiResponse.CreateError(
                        invalidEx.Message,
                        "INVALID_OPERATION"
                    )
                },

                NotSupportedException notSupportedEx => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    ApiResponse = ApiResponse.CreateError(
                        notSupportedEx.Message,
                        "NOT_SUPPORTED"
                    )
                },

                TimeoutException => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.RequestTimeout,
                    ApiResponse = ApiResponse.CreateError(
                        "请求超时",
                        "TIMEOUT"
                    )
                },

                TaskCanceledException => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.RequestTimeout,
                    ApiResponse = ApiResponse.CreateError(
                        "请求被取消",
                        "CANCELLED"
                    )
                },

                OutOfMemoryException => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.InsufficientStorage,
                    ApiResponse = ApiResponse.CreateError(
                        "系统内存不足",
                        "OUT_OF_MEMORY"
                    )
                },

                IOException ioEx => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    ApiResponse = ApiResponse.CreateError(
                        $"文件操作错误: {ioEx.Message}",
                        "IO_ERROR"
                    )
                },

                _ => new ErrorResponse
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    ApiResponse = ApiResponse.CreateError(
                        _environment.IsDevelopment()
                            ? $"服务器内部错误: {exception.Message}"
                            : "服务器内部错误",
                        "INTERNAL_ERROR"
                    )
                }
            };
        }

        private class ErrorResponse
        {
            public int StatusCode { get; set; }
            public ApiResponse ApiResponse { get; set; } = new();
        }
    }

    /// <summary>
    /// 全局异常处理中间件扩展
    /// </summary>
    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }

    /// <summary>
    /// 请求日志中间件
    /// </summary>
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var startTime = DateTime.UtcNow;
            var requestId = Guid.NewGuid().ToString("N")[..8];

            // 记录请求开始
            _logger.LogInformation(
                "[{RequestId}] {Method} {Path} 开始处理",
                requestId,
                context.Request.Method,
                context.Request.Path
            );

            try
            {
                await _next(context);
            }
            finally
            {
                var duration = DateTime.UtcNow - startTime;
                
                // 记录请求完成
                _logger.LogInformation(
                    "[{RequestId}] {Method} {Path} 处理完成 - 状态码: {StatusCode}, 耗时: {Duration}ms",
                    requestId,
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    duration.TotalMilliseconds
                );
            }
        }
    }

    /// <summary>
    /// 请求日志中间件扩展
    /// </summary>
    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}
