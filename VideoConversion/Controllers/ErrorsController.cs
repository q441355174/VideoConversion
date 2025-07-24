using Microsoft.AspNetCore.Mvc;
using VideoConversion.Controllers.Base;

namespace VideoConversion.Controllers
{
    /// <summary>
    /// 错误处理控制器
    /// </summary>
    [Route("api/[controller]")]
    public class ErrorsController : BaseApiController
    {
        public ErrorsController(ILogger<ErrorsController> logger) : base(logger)
        {

        }

        /// <summary>
        /// 报告错误
        /// </summary>
        [HttpPost("report")]
        public async Task<IActionResult> ReportError([FromBody] ErrorReportRequest request)
        {
            if (request == null)
                return ValidationError("错误报告不能为空");

            return await SafeExecuteAsync(
                async () =>
                {
                    // 记录错误到日志
                    Logger.LogError("客户端错误报告: {ErrorType} - {Message}", 
                        request.Type, request.Message);
                    
                    // 这里可以将错误保存到数据库或发送到错误监控服务
                    await Task.Delay(100); // 模拟保存过程
                    
                    return new
                    {
                        reportId = Guid.NewGuid().ToString(),
                        message = "错误报告已收到",
                        timestamp = DateTime.Now
                    };
                },
                "报告错误",
                "错误报告已成功提交"
            );
        }

        /// <summary>
        /// 提交用户反馈
        /// </summary>
        [HttpPost("feedback")]
        public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackRequest request)
        {
            if (request == null)
                return ValidationError("反馈内容不能为空");

            return await SafeExecuteAsync(
                async () =>
                {
                    // 记录用户反馈
                    Logger.LogInformation("用户反馈: ErrorId={ErrorId}, Feedback={Feedback}", 
                        request.ErrorId, request.UserFeedback);
                    
                    // 这里可以将反馈保存到数据库
                    await Task.Delay(100);
                    
                    return new
                    {
                        feedbackId = Guid.NewGuid().ToString(),
                        message = "感谢您的反馈",
                        timestamp = DateTime.Now
                    };
                },
                "提交用户反馈",
                "用户反馈已成功提交"
            );
        }

        /// <summary>
        /// 获取错误统计
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetErrorStatistics()
        {
            return await SafeExecuteAsync(
                async () =>
                {
                    await Task.Delay(100);
                    
                    // 模拟错误统计数据
                    return new
                    {
                        totalErrors = 42,
                        todayErrors = 5,
                        errorTypes = new[]
                        {
                            new { type = "JavaScript Error", count = 15 },
                            new { type = "Network Error", count = 12 },
                            new { type = "Promise Rejection", count = 8 },
                            new { type = "Resource Error", count = 5 },
                            new { type = "Application Error", count = 2 }
                        },
                        lastUpdated = DateTime.Now
                    };
                },
                "获取错误统计",
                "错误统计获取成功"
            );
        }
    }

    /// <summary>
    /// 错误报告请求模型
    /// </summary>
    public class ErrorReportRequest
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Stack { get; set; }
        public string? Url { get; set; }
        public string? UserAgent { get; set; }
        public DateTime Timestamp { get; set; }
        public string? SessionId { get; set; }
        public object? BrowserInfo { get; set; }
    }

    /// <summary>
    /// 用户反馈请求模型
    /// </summary>
    public class FeedbackRequest
    {
        public string ErrorId { get; set; } = string.Empty;
        public string UserFeedback { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
