using Microsoft.AspNetCore.Mvc;
using VideoConversion.Controllers.Base;
using VideoConversion.Services;
using VideoConversion.Models;

namespace VideoConversion.Controllers
{
    /// <summary>
    /// 任务管理控制器 - 演示优化后的控制器架构
    /// 职责：专门处理任务查询、列表、详情等功能
    /// </summary>
    [Route("api/[controller]")]
    public class TaskController : BaseApiController
    {
        private readonly DatabaseService _databaseService;
        private readonly LoggingService _loggingService;

        public TaskController(
            DatabaseService databaseService,
            LoggingService loggingService,
            ILogger<TaskController> logger) : base(logger)
        {
            _databaseService = databaseService;
            _loggingService = loggingService;
        }

        /// <summary>
        /// 获取任务状态 - 演示使用基类的 SafeExecuteAsync 方法
        /// </summary>
        [HttpGet("status/{taskId}")]
        public async Task<IActionResult> GetTaskStatus(string taskId)
        {
            // 使用基类的验证方法
            if (string.IsNullOrWhiteSpace(taskId))
                return ValidationError("任务ID不能为空");

            // 使用基类的安全执行方法，自动处理异常和响应格式
            return await SafeExecuteAsync(
                async () =>
                {
                    var task = await _databaseService.GetTaskAsync(taskId);
                    if (task == null)
                    {
                        throw new FileNotFoundException("任务不存在");
                    }

                    // 使用状态映射服务返回统一格式的数据
                    return StatusMappingService.CreateDetailedTaskInfo(task);
                },
                "获取任务状态",
                "任务状态获取成功"
            );
        }

        /// <summary>
        /// 获取最近的任务 - 演示参数验证和列表返回
        /// </summary>
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentTasks([FromQuery] int count = 10)
        {
            // 使用基类的验证方法
            if (count < 1 || count > 100)
                return ValidationError("数量必须在1-100之间");

            return await SafeExecuteAsync(
                async () =>
                {
                    var tasks = await _databaseService.GetAllTasksAsync(1, count);
                    // 使用状态映射服务返回统一格式的数据
                    return tasks.Select(StatusMappingService.CreateSimpleTaskInfo).ToList();
                },
                "获取最近任务",
                "最近任务获取成功"
            );
        }

        /// <summary>
        /// 获取任务列表（支持分页）- 演示分页响应
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> GetTasks(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
            [FromQuery] string? search = null)
        {
            // 使用基类的分页验证方法
            if (!IsValidPagination(page, pageSize, out var error))
                return ValidationError(error);

            return await SafeExecutePagedAsync(
                async () =>
                {
                    var tasks = await _databaseService.GetAllTasksAsync(page, pageSize);
                    var totalCount = await _databaseService.GetTaskCountAsync();

                    var taskList = tasks.Select(t => new
                    {
                        t.Id,
                        t.TaskName,
                        t.Status,
                        t.Progress,
                        t.CreatedAt,
                        t.StartedAt,
                        t.CompletedAt,
                        t.OriginalFileName,
                        t.OutputFileName,
                        t.InputFormat,
                        t.OutputFormat,
                        t.OriginalFileSize,
                        t.OutputFileSize,
                        t.ErrorMessage,
                        t.VideoCodec,
                        t.AudioCodec,
                        t.Resolution,
                        t.FrameRate
                    }).ToList();

                    return PagedApiResponse<object>.CreateSuccess(taskList, page, pageSize, totalCount);
                },
                "获取任务列表"
            );
        }

        /// <summary>
        /// 删除任务 - 演示业务逻辑验证和操作记录
        /// </summary>
        [HttpDelete("{taskId}")]
        public async Task<IActionResult> DeleteTask(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
                return ValidationError("任务ID不能为空");

            return await SafeExecuteAsync(
                async () =>
                {
                    var task = await _databaseService.GetTaskAsync(taskId);
                    if (task == null)
                    {
                        throw new FileNotFoundException("任务不存在");
                    }

                    // 业务逻辑验证
                    if (task.Status == ConversionStatus.Converting)
                    {
                        throw new InvalidOperationException("无法删除正在进行的转换任务，请先取消任务");
                    }

                    await _databaseService.DeleteTaskAsync(taskId);

                    // 记录操作日志
                    Logger.LogInformation("任务删除成功 - TaskId: {TaskId}, TaskName: {TaskName}, ClientIP: {ClientIP}",
                        taskId, task.TaskName, GetClientIpAddress());

                    return new { taskId = taskId, taskName = task.TaskName };
                },
                "删除任务",
                "任务删除成功"
            );
        }

        /// <summary>
        /// 清理旧任务 - 演示批量操作
        /// </summary>
        [HttpPost("cleanup")]
        public async Task<IActionResult> CleanupOldTasks([FromQuery] int daysOld = 30)
        {
            if (daysOld < 1 || daysOld > 365)
                return ValidationError("清理天数必须在1-365之间");

            return await SafeExecuteAsync(
                async () =>
                {
                    var deletedCount = await _databaseService.CleanupOldTasksAsync(daysOld);

                    // 记录清理操作
                    _loggingService.LogCleanupOperation("任务清理", deletedCount, TimeSpan.Zero);
                    Logger.LogInformation("任务清理完成 - DeletedCount: {DeletedCount}, DaysOld: {DaysOld}, ClientIP: {ClientIP}",
                        deletedCount, daysOld, GetClientIpAddress());

                    return new { deletedCount = deletedCount, daysOld = daysOld };
                },
                "清理旧任务",
                $"成功清理了 {daysOld} 天前的旧任务"
            );
        }
    }
}
