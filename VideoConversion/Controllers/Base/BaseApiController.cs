using Microsoft.AspNetCore.Mvc;
using VideoConversion.Models;

namespace VideoConversion.Controllers.Base
{
    /// <summary>
    /// 基础API控制器 - 提供统一的响应格式和通用功能
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public abstract class BaseApiController : ControllerBase
    {
        protected readonly ILogger Logger;
        protected BaseApiController(ILogger logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// 返回成功响应
        /// </summary>
        protected IActionResult Success<T>(T data, string message = "操作成功")
        {
            return Ok(new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// 返回成功响应（无数据）
        /// </summary>
        protected IActionResult Success(string message = "操作成功")
        {
            return Ok(new ApiResponse
            {
                Success = true,
                Message = message,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// 返回错误响应
        /// </summary>
        protected IActionResult Error(string message, int statusCode = 400)
        {
            Logger.LogWarning("API错误响应: {Message}", message);
            
            return StatusCode(statusCode, new ApiResponse
            {
                Success = false,
                Message = message,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// 返回验证错误响应
        /// </summary>
        protected IActionResult ValidationError(string message = "请求参数无效")
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = message,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// 返回未找到响应
        /// </summary>
        protected IActionResult NotFound(string message = "请求的资源未找到")
        {
            return NotFound(new ApiResponse
            {
                Success = false,
                Message = message,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// 返回服务器错误响应
        /// </summary>
        protected IActionResult ServerError(string message = "服务器内部错误")
        {
            Logger.LogError("服务器错误: {Message}", message);
            
            return StatusCode(500, new ApiResponse
            {
                Success = false,
                Message = message,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// 验证模型状态
        /// </summary>
        protected bool ValidateModel()
        {
            return ModelState.IsValid;
        }

        /// <summary>
        /// 获取模型验证错误信息
        /// </summary>
        protected string GetModelErrors()
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors)
                .Select(x => x.ErrorMessage);
            
            return string.Join("; ", errors);
        }

        /// <summary>
        /// 安全执行异步操作
        /// </summary>
        protected async Task<IActionResult> SafeExecuteAsync<T>(
            Func<Task<T>> operation, 
            string operationName,
            string? successMessage = null)
        {
            try
            {
                Logger.LogInformation("开始执行操作: {OperationName}", operationName);
                
                var result = await operation();
                
                Logger.LogInformation("操作执行成功: {OperationName}", operationName);
                
                return Success(result, successMessage ?? "操作成功");
            }
            catch (ArgumentException ex)
            {
                Logger.LogWarning(ex, "操作参数错误: {OperationName}", operationName);
                return ValidationError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogWarning(ex, "操作状态错误: {OperationName}", operationName);
                return Error(ex.Message, 409);
            }
            catch (FileNotFoundException ex)
            {
                Logger.LogWarning(ex, "文件未找到: {OperationName}", operationName);
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogWarning(ex, "访问被拒绝: {OperationName}", operationName);
                return StatusCode(403, new ApiResponse
                {
                    Success = false,
                    Message = "访问被拒绝",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "操作执行失败: {OperationName}", operationName);
                return ServerError($"执行{operationName}时发生错误");
            }
        }

        /// <summary>
        /// 安全执行同步操作
        /// </summary>
        protected IActionResult SafeExecute<T>(
            Func<T> operation, 
            string operationName,
            string? successMessage = null)
        {
            try
            {
                Logger.LogInformation("开始执行操作: {OperationName}", operationName);
                
                var result = operation();
                
                Logger.LogInformation("操作执行成功: {OperationName}", operationName);
                
                return Success(result, successMessage ?? "操作成功");
            }
            catch (ArgumentException ex)
            {
                Logger.LogWarning(ex, "操作参数错误: {OperationName}", operationName);
                return ValidationError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogWarning(ex, "操作状态错误: {OperationName}", operationName);
                return Error(ex.Message, 409);
            }
            catch (FileNotFoundException ex)
            {
                Logger.LogWarning(ex, "文件未找到: {OperationName}", operationName);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "操作执行失败: {OperationName}", operationName);
                return ServerError($"执行{operationName}时发生错误");
            }
        }

        /// <summary>
        /// 验证任务ID格式
        /// </summary>
        protected bool IsValidTaskId(string taskId)
        {
            return !string.IsNullOrWhiteSpace(taskId) && 
                   taskId.Length >= 10 && 
                   taskId.Length <= 50;
        }

        /// <summary>
        /// 验证分页参数
        /// </summary>
        protected bool IsValidPagination(int page, int pageSize, out string errorMessage)
        {
            errorMessage = "";

            if (page < 1)
            {
                errorMessage = "页码必须大于0";
                return false;
            }

            if (pageSize < 1 || pageSize > 100)
            {
                errorMessage = "每页大小必须在1-100之间";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 安全执行异步操作，返回分页结果
        /// </summary>
        protected async Task<IActionResult> SafeExecutePagedAsync<T>(
            Func<Task<PagedApiResponse<T>>> operation,
            string operationName)
        {
            try
            {
                Logger.LogInformation("开始执行分页操作: {OperationName}", operationName);

                var result = await operation();

                Logger.LogInformation("分页操作执行成功: {OperationName}", operationName);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                Logger.LogWarning(ex, "分页参数验证失败: {OperationName}", operationName);
                return ValidationError(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "分页操作执行失败: {OperationName}", operationName);
                return ServerError($"执行{operationName}时发生错误");
            }
        }

        /// <summary>
        /// 获取客户端IP地址
        /// </summary>
        protected string GetClientIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }
    }
}
