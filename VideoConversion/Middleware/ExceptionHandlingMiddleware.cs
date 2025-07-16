using System.Net;
using System.Text.Json;

namespace VideoConversion.Middleware
{
    /// <summary>
    /// 全局异常处理中间件
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "未处理的异常发生在 {Path}", context.Request.Path);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            
            var response = new ErrorResponse();

            switch (exception)
            {
                case FileNotFoundException:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Message = "请求的文件不存在";
                    break;
                
                case UnauthorizedAccessException:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.Message = "访问被拒绝";
                    break;
                
                case ArgumentException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Message = "请求参数无效";
                    break;
                
                case InvalidOperationException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Message = "操作无效";
                    break;
                
                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response.Message = "服务器内部错误";
                    break;
            }

            context.Response.StatusCode = response.StatusCode;

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }

    /// <summary>
    /// 错误响应模型
    /// </summary>
    public class ErrorResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 中间件扩展方法
    /// </summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
