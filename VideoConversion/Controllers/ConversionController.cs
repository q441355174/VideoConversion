using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VideoConversion.Models;
using VideoConversion.Services;
using VideoConversion.Hubs;
using VideoConversion.Controllers.Base;
using System.ComponentModel.DataAnnotations;

namespace VideoConversion.Controllers
{
    [Route("api/[controller]")]
    public class ConversionController : BaseApiController
    {
        private readonly DatabaseService _databaseService;
        private readonly FileService _fileService;
        private readonly VideoConversionService _conversionService;
        private readonly LoggingService _loggingService;
        private readonly FFmpegFormatDetectionService _formatDetectionService;
        private readonly BatchTaskSpaceControlService _batchTaskService;
        private readonly DownloadTrackingService _downloadTrackingService;
        private readonly ILogger<ConversionController> _logger;
        private readonly IHubContext<ConversionHub> _hubContext;

        public ConversionController(
            DatabaseService databaseService,
            FileService fileService,
            VideoConversionService conversionService,
            LoggingService loggingService,
            FFmpegFormatDetectionService formatDetectionService,
            BatchTaskSpaceControlService batchTaskService,
            DownloadTrackingService downloadTrackingService,
            ILogger<ConversionController> logger,
            IHubContext<ConversionHub> hubContext) : base(logger)
        {
            _databaseService = databaseService;
            _fileService = fileService;
            _conversionService = conversionService;
            _loggingService = loggingService;
            _formatDetectionService = formatDetectionService;
            _batchTaskService = batchTaskService;
            _downloadTrackingService = downloadTrackingService;
            _logger = logger;
            _hubContext = hubContext;
        }

        /// <summary>
        /// 开始转换任务 - 已优化（保持原有逻辑，增强验证和日志）
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartConversion([FromForm] StartConversionRequest request)
        {
            // 使用基类的参数验证
            if (request == null)
                return ValidationError("请求参数不能为空");

            if (request.VideoFile == null)
                return ValidationError("视频文件不能为空");

            Logger.LogInformation("开始处理转换任务请求: TaskName={TaskName}, OutputFormat={OutputFormat}",
                request.TaskName, request.OutputFormat);

            return await ProcessConversionRequest(request, request.VideoFile);
        }

        /// <summary>
        /// 从已上传文件开始转换任务 - 已优化（增强验证和日志）
        /// </summary>
        [HttpPost("start-from-upload")]
        public async Task<IActionResult> StartConversionFromUpload([FromForm] StartConversionFromUploadRequest request)
        {
            try
            {
                // 使用基类的参数验证
                if (request == null)
                    return ValidationError("请求参数不能为空");

                // 验证上传文件路径
                if (string.IsNullOrEmpty(request.UploadedFilePath) || !System.IO.File.Exists(request.UploadedFilePath))
                {
                    return ValidationError("上传文件不存在");
                }

                Logger.LogInformation("开始处理从上传文件的转换任务: FilePath={FilePath}, TaskName={TaskName}",
                    request.UploadedFilePath, request.TaskName);

                // 创建虚拟IFormFile对象
                var fileInfo = new FileInfo(request.UploadedFilePath);
                var formFile = new UploadedFormFile(request.UploadedFilePath, request.OriginalFileName ?? fileInfo.Name, fileInfo.Length);

                // 转换请求对象
                var conversionRequest = new StartConversionRequest
                {
                    VideoFile = formFile,
                    TaskName = request.TaskName,
                    Preset = request.Preset,
                    OutputFormat = request.OutputFormat,
                    VideoCodec = request.VideoCodec,
                    AudioCodec = request.AudioCodec,
                    VideoQuality = request.VideoQuality,
                    AudioQuality = request.AudioQuality,
                    Resolution = request.Resolution,
                    FrameRate = request.FrameRate,
                    QualityMode = request.QualityMode,
                    AudioQualityMode = request.AudioQualityMode,
                    TwoPass = request.TwoPass,
                    FastStart = request.FastStart,
                    CopyTimestamps = request.CopyTimestamps,
                    CustomParameters = request.CustomParameters,
                    HardwareAcceleration = request.HardwareAcceleration,
                    VideoFilters = request.VideoFilters,
                    AudioFilters = request.AudioFilters,
                    StartTime = request.StartTime?.ToString(),
                    EndTime = request.EndTime,
                    Priority = request.Priority,
                    Tags = request.Tags,
                    Notes = request.Notes,
                    Profile = request.Profile,
                    AudioChannels = request.AudioChannels,
                    SampleRate = request.SampleRate,
                    AudioVolume = request.AudioVolume,
                    Deinterlace = request.Deinterlace,
                    Denoise = request.Denoise,
                    ColorSpace = request.ColorSpace,
                    PixelFormat = request.PixelFormat
                };

                return await ProcessConversionRequest(conversionRequest, formFile, true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "从上传文件创建转换任务失败");
                return StatusCode(500, new { success = false, message = "创建转换任务失败: " + ex.Message });
            }
        }

        /// <summary>
        /// 处理转换请求的通用方法
        /// </summary>
        private async Task<IActionResult> ProcessConversionRequest(StartConversionRequest request, IFormFile file, bool isFromUpload = false)
        {
            try
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                _logger.LogInformation("=== 开始处理转换请求 ===");
                _logger.LogInformation("客户端IP: {ClientIP}", clientIp);
                _logger.LogInformation("请求文件: {FileName}", request.VideoFile?.FileName);
                _logger.LogInformation("任务名称: {TaskName}", request.TaskName);
                _logger.LogInformation("预设: {Preset}", request.Preset);

                // 检查模型状态
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("模型验证失败:");
                    foreach (var error in ModelState)
                    {
                        _logger.LogWarning("字段 {Field}: {Errors}", error.Key, string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
                    }
                    return BadRequest(ModelState);
                }

                // 记录文件上传事件
                if (request.VideoFile != null)
                {
                    _loggingService.LogFileUploaded(request.VideoFile.FileName, request.VideoFile.Length, clientIp);
                }

                // 验证文件
                if (request.VideoFile == null)
                {
                    _logger.LogError("文件验证失败: 未选择文件");
                    return BadRequest(new { success = false, message = "未选择文件" });
                }

                _logger.LogInformation("开始文件验证: {FileName} ({FileSize} bytes)",
                    request.VideoFile.FileName, request.VideoFile.Length);

                var validation = _fileService.ValidateFile(request.VideoFile);
                if (!validation.IsValid)
                {
                    _logger.LogWarning("文件验证失败: {Error}", validation.ErrorMessage);
                    return BadRequest(new { success = false, message = validation.ErrorMessage });
                }

                _logger.LogInformation("文件验证通过，开始保存文件: {FileName}", request.VideoFile.FileName);

                // 保存上传的文件
                _logger.LogInformation("开始保存文件到服务器...");
                var saveResult = await _fileService.SaveUploadedFileAsync(request.VideoFile);
                if (!saveResult.Success)
                {
                    _logger.LogError("文件保存失败: {Error}", saveResult.ErrorMessage);
                    return BadRequest(new { success = false, message = saveResult.ErrorMessage });
                }
                 
                _logger.LogInformation("文件保存成功: {FilePath}", saveResult.FilePath);

                // 获取转换预设作为基础
                _logger.LogInformation("获取转换预设: {PresetName}", request.Preset);
                var preset = ConversionPreset.GetPresetByName(request.Preset) ?? ConversionPreset.GetDefaultPreset();
                _logger.LogInformation("使用预设: {PresetName} -> {OutputFormat}", preset.Name, preset.OutputFormat);

                // 应用详细的自定义设置
                _logger.LogInformation("应用自定义设置...");
                ApplyCustomSettings(preset, request);

                // 生成输出文件路径
                _logger.LogInformation("生成输出文件路径...");
                var outputFilePath = _fileService.GenerateOutputFilePath(
                    request.VideoFile.FileName,
                    preset.OutputFormat,
                    request.TaskName);
                _logger.LogInformation("输出文件路径: {OutputPath}", outputFilePath);

                // 创建转换任务
                _logger.LogInformation("开始创建转换任务对象...");
                var task = new ConversionTask
                {
                    TaskName = !string.IsNullOrEmpty(request.TaskName) ? request.TaskName : Path.GetFileNameWithoutExtension(request.VideoFile.FileName) ?? "未命名任务",
                    OriginalFileName = request.VideoFile.FileName ?? "unknown.mkv",
                    OriginalFilePath = saveResult.FilePath ?? string.Empty,
                    OutputFileName = Path.GetFileName(outputFilePath) ?? "output.mp4",
                    OutputFilePath = outputFilePath ?? string.Empty,
                    OriginalFileSize = request.VideoFile.Length,
                    OutputFileSize = 0, // 初始化为0
                    InputFormat = Path.GetExtension(request.VideoFile.FileName)?.TrimStart('.') ?? "mkv",
                    OutputFormat = preset.OutputFormat ?? "mp4",
                    VideoCodec = preset.VideoCodec ?? "libx264",
                    AudioCodec = preset.AudioCodec ?? "aac",
                    VideoQuality = preset.VideoQuality ?? "23",
                    AudioQuality = preset.AudioQuality ?? "128k",
                    Resolution = preset.Resolution ?? "1920x1080",
                    FrameRate = preset.FrameRate ?? "30",
                    Status = ConversionStatus.Pending,
                    Progress = 0,
                    ErrorMessage = string.Empty,

                    // 扩展参数 - 现在模型中都有默认值
                    EncodingPreset = request.EncodingPreset ?? string.Empty,
                    Profile = request.Profile ?? string.Empty,
                    AudioChannels = request.AudioChannels ?? string.Empty,
                    SampleRate = request.SampleRate ?? string.Empty,
                    AudioVolume = request.AudioVolume?.ToString() ?? string.Empty,
                    StartTime = !string.IsNullOrEmpty(request.StartTime) && double.TryParse(request.StartTime, out var startTime) ? startTime : null,
                    DurationLimit = !string.IsNullOrEmpty(request.Duration) && double.TryParse(request.Duration, out var duration) ? duration : null,
                    Deinterlace = request.Deinterlace,
                    Denoise = request.Denoise ?? string.Empty,
                    ColorSpace = request.ColorSpace ?? string.Empty,
                    PixelFormat = request.PixelFormat ?? string.Empty,
                    CustomParams = request.CustomParams ?? string.Empty,
                    TwoPass = request.TwoPass,
                    FastStart = request.FastStart,
                    CopyTimestamps = request.CopyTimestamps,
                    QualityMode = request.QualityMode,
                    AudioQualityMode = request.AudioQualityMode,
                    CreatedAt = DateTime.Now
                };

                _logger.LogInformation("任务对象创建完成");
                _logger.LogInformation("任务ID: {TaskId}", task.Id);
                _logger.LogInformation("任务名称: {TaskName}", task.TaskName);
                _logger.LogInformation("输入格式: {InputFormat}", task.InputFormat);
                _logger.LogInformation("输出格式: {OutputFormat}", task.OutputFormat);
                _logger.LogInformation("视频编码: {VideoCodec}", task.VideoCodec);
                _logger.LogInformation("音频编码: {AudioCodec}", task.AudioCodec);

                // 保存任务到数据库
                _logger.LogInformation("开始保存任务到数据库...");
                var dbStartTime = DateTime.Now;
                await _databaseService.CreateTaskAsync(task);
                var dbDuration = DateTime.Now - dbStartTime;

                _loggingService.LogDatabaseOperation("INSERT", "ConversionTasks", 1, dbDuration);
                _logger.LogInformation("任务保存到数据库成功: {TaskId} - {TaskName} (耗时: {Duration}ms)",
                    task.Id, task.TaskName, dbDuration.TotalMilliseconds);

                // 记录转换任务开始
                _loggingService.LogConversionStarted(task.Id, task.TaskName, task.OriginalFilePath, task.OutputFormat);

                var response = new {
                    success = true,
                    taskId = task.Id,
                    taskName = task.TaskName,
                    message = "转换任务已创建，正在队列中等待处理"
                };

                _logger.LogInformation("转换任务创建成功!");
                _logger.LogInformation("响应数据: {@Response}", response);

                // 通知所有客户端有新任务创建
                try
                {
                    await _hubContext.Clients.All.SendAsync("TaskCreated", new
                    {
                        TaskId = task.Id,
                        TaskName = task.TaskName,
                        Status = task.Status.ToString(),
                        CreatedAt = task.CreatedAt,
                        Timestamp = DateTime.Now
                    });
                    // 新任务创建通知已发送
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "发送新任务创建通知失败");
                }

                // 注册到批量任务管理（单个任务也作为批量任务处理）
                try
                {
                    var clientId = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    var batchId = await _batchTaskService.RegisterBatchTaskAsync(new List<string> { task.Id }, clientId);
                    _logger.LogInformation("📦 任务已注册到批量任务管理: TaskId={TaskId}, BatchId={BatchId}", task.Id, batchId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "注册批量任务失败: {TaskId}", task.Id);
                    // 不影响主流程，继续执行
                }

                _logger.LogInformation("=== 转换请求处理完成 ===");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建转换任务失败");
                _logger.LogError("错误详情: {ErrorMessage}", ex.Message);
                _logger.LogError("堆栈跟踪: {StackTrace}", ex.StackTrace);

                return StatusCode(500, new {
                    success = false,
                    message = "服务器内部错误: " + ex.Message
                });
            }
        }

        // 注意：任务列表查询功能已移至 TaskController
        // 请使用 /api/task/list 端点

        // 注意：任务状态查询功能已移至 TaskController
        // 请使用 /api/task/status/{taskId} 端点

        /// <summary>
        /// 获取转换预设配置
        /// </summary>
        [HttpGet("presets")]
        public IActionResult GetPresets()
        {
            try
            {
                var presets = ConversionPreset.GetAllPresets();

                // 转换为字典格式，方便前端使用
                var presetDict = presets.ToDictionary(
                    p => p.Name,
                    p => new
                    {
                        p.Name,
                        p.Description,
                        p.OutputFormat,
                        p.VideoCodec,
                        p.AudioCodec,
                        p.VideoQuality,
                        p.AudioQuality,
                        p.Resolution,
                        p.FrameRate,
                        p.IsDefault
                    }
                );

                return Success(presetDict, "预设配置获取成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取预设配置失败");
                return Error("获取预设配置失败");
            }
        }

        /// <summary>
        /// 获取最近的任务 - 已优化使用 BaseApiController
        /// </summary>
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentTasks([FromQuery] int count = 10)
        {
            // 使用基类的参数验证
            if (count < 1 || count > 100)
                return ValidationError("数量必须在1-100之间");

            // 使用基类的安全执行方法
            return await SafeExecuteAsync(
                async () =>
                {
                    var tasks = await _databaseService.GetAllTasksAsync(1, count);
                    return tasks.Select(t => new
                    {
                        t.Id,
                        t.TaskName,
                        t.Status,
                        t.Progress,
                        t.CreatedAt,
                        t.CompletedAt,
                        t.OriginalFileName,
                        t.OutputFileName,
                        t.InputFormat,
                        t.OutputFormat,
                        t.OriginalFileSize,
                        t.OutputFileSize,
                        t.ErrorMessage
                    }).ToList();
                },
                "获取最近任务",
                "最近任务获取成功"
            );
        }

        /// <summary>
        /// 下载转换后的文件 - 已优化使用 BaseApiController（特殊处理文件下载）
        /// </summary>
        [HttpGet("download/{taskId}")]
        public async Task<IActionResult> DownloadFile(string taskId)
        {
            // 使用基类的参数验证
            if (string.IsNullOrWhiteSpace(taskId))
                return ValidationError("任务ID不能为空");

            try
            {
                Logger.LogInformation("开始处理文件下载请求: {TaskId}", taskId);

                var task = await _databaseService.GetTaskAsync(taskId);
                if (task == null)
                {
                    Logger.LogWarning("下载请求的任务不存在: {TaskId}", taskId);
                    return NotFound(ApiResponse<object>.CreateError("任务不存在"));
                }

                if (task.Status != ConversionStatus.Completed || string.IsNullOrEmpty(task.OutputFilePath))
                {
                    Logger.LogWarning("任务文件尚未准备好下载: {TaskId}, Status: {Status}", taskId, task.Status);
                    return BadRequest(ApiResponse<object>.CreateError("文件尚未准备好下载"));
                }

                // 使用原始文件名生成用户友好的下载文件名
                var downloadResult = await _fileService.GetFileDownloadStreamAsync(
                    task.OutputFilePath,
                    task.OriginalFileName,
                    includeConvertedSuffix: true);

                if (downloadResult.Stream == null)
                {
                    Logger.LogWarning("下载文件不存在: {TaskId}, Path: {Path}", taskId, task.OutputFilePath);
                    return NotFound(ApiResponse<object>.CreateError("文件不存在"));
                }

                // 记录下载日志
                _loggingService.LogFileDownloaded(taskId, downloadResult.FileName, GetClientIpAddress());
                Logger.LogInformation("文件下载成功: {TaskId}, OriginalFileName: {OriginalFileName}, DownloadFileName: {DownloadFileName}",
                    taskId, task.OriginalFileName, downloadResult.FileName);

                // 跟踪下载（异步执行，不阻塞下载）
                var clientIp = GetClientIpAddress();
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _downloadTrackingService.TrackDownloadAsync(taskId, clientIp, userAgent);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "跟踪下载失败: {TaskId}", taskId);
                    }
                });

                return File(downloadResult.Stream, downloadResult.ContentType, downloadResult.FileName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "下载文件失败: {TaskId}", taskId);
                return ServerError("下载文件时发生错误");
            }
        }

        /// <summary>
        /// 取消转换任务 - 已优化使用 BaseApiController
        /// </summary>
        [HttpPost("cancel/{taskId}")]
        public async Task<IActionResult> CancelTask(string taskId)
        {
            // 使用基类的参数验证
            if (string.IsNullOrWhiteSpace(taskId))
                return ValidationError("任务ID不能为空");

            return await SafeExecuteAsync(
                async () =>
                {
                    await _conversionService.CancelConversionAsync(taskId);
                    return new { taskId = taskId, status = "cancelled" };
                },
                "取消转换任务",
                "任务已成功取消"
            );
        }

        /// <summary>
        /// 获取正在运行的进程信息 - 已优化使用 BaseApiController
        /// </summary>
        [HttpGet("processes")]
        public async Task<IActionResult> GetRunningProcesses()
        {
            return await SafeExecuteAsync(
                async () =>
                {
                    await Task.CompletedTask; // 占位符，因为原方法是同步的
                    var statistics = _conversionService.GetProcessStatistics();
                    return statistics;
                },
                "获取进程信息",
                "进程信息获取成功"
            );
        }

        /// <summary>
        /// 检查任务是否正在运行 - 已优化使用 BaseApiController
        /// </summary>
        [HttpGet("is-running/{taskId}")]
        public async Task<IActionResult> IsTaskRunning(string taskId)
        {
            // 使用基类的参数验证
            if (string.IsNullOrWhiteSpace(taskId))
                return ValidationError("任务ID不能为空");

            // 使用基类的安全执行方法（同步操作包装为异步）
            return await SafeExecuteAsync(
                async () =>
                {
                    await Task.CompletedTask; // 占位符，因为原方法是同步的
                    var isRunning = _conversionService.IsTaskRunning(taskId);
                    return new { isRunning = isRunning, taskId = taskId };
                },
                "检查任务运行状态",
                "任务运行状态检查完成"
            );
        }

        /// <summary>
        /// 获取任务详细信息（包括编码器设置）- 已优化使用 BaseApiController
        /// </summary>
        [HttpGet("task-details/{taskId}")]
        public async Task<IActionResult> GetTaskDetails(string taskId)
        {
            // 使用基类的参数验证
            if (string.IsNullOrWhiteSpace(taskId))
                return ValidationError("任务ID不能为空");

            // 使用基类的安全执行方法
            return await SafeExecuteAsync(
                async () =>
                {
                    var task = await _databaseService.GetTaskAsync(taskId);
                    if (task == null)
                    {
                        throw new FileNotFoundException("任务不存在");
                    }

                    // 返回详细的任务信息
                    return new
                    {
                        task.Id,
                        task.TaskName,
                        task.Status,
                        task.Progress,
                        task.VideoCodec,
                        task.AudioCodec,
                        task.OutputFormat,
                        task.QualityMode,
                        task.VideoQuality,
                        task.AudioQuality,
                        task.Resolution,
                        task.FrameRate,
                        task.OriginalFilePath,
                        task.OutputFilePath,
                        task.CreatedAt,
                        task.CompletedAt,
                        task.ErrorMessage,
                        // 添加更多详细信息
                        task.OriginalFileName,
                        task.OutputFileName,
                        task.InputFormat,
                        task.OriginalFileSize,
                        task.OutputFileSize,
                        task.Duration,
                        task.ConversionSpeed,
                        task.StartedAt,
                        task.EstimatedTimeRemaining
                    };
                },
                "获取任务详细信息",
                "任务详细信息获取成功"
            );
        }

        /// <summary>
        /// 删除任务 - 已优化使用 BaseApiController
        /// </summary>
        [HttpDelete("{taskId}")]
        public async Task<IActionResult> DeleteTask(string taskId)
        {
            // 使用基类的参数验证
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

                    // 删除相关文件
                    if (!string.IsNullOrEmpty(task.OriginalFilePath))
                    {
                        await _fileService.DeleteFileAsync(task.OriginalFilePath);
                    }

                    if (!string.IsNullOrEmpty(task.OutputFilePath))
                    {
                        await _fileService.DeleteFileAsync(task.OutputFilePath);
                    }

                    // 删除数据库记录
                    await _databaseService.DeleteTaskAsync(taskId);

                    return new { taskId = taskId, taskName = task.TaskName };
                },
                "删除任务",
                "任务已成功删除"
            );
        }

        // 注意：任务列表查询功能已移至 TaskController
        // 请使用 /api/task/list 端点

        /// <summary>
        /// 清理旧任务 - 已优化使用 BaseApiController
        /// </summary>
        [HttpPost("cleanup")]
        public async Task<IActionResult> CleanupOldTasks([FromQuery] int daysOld = 30)
        {
            // 使用基类的参数验证
            if (daysOld < 1 || daysOld > 365)
                return ValidationError("清理天数必须在1-365之间");

            return await SafeExecuteAsync(
                async () =>
                {
                    var deletedTaskCount = await _databaseService.CleanupOldTasksAsync(daysOld);
                    var deletedFileCount = await _fileService.CleanupOldFilesAsync(daysOld);

                    return new
                    {
                        deletedTaskCount = deletedTaskCount,
                        deletedFileCount = deletedFileCount,
                        daysOld = daysOld,
                        cleanupDate = DateTime.Now
                    };
                },
                "清理旧任务",
                $"成功清理了 {daysOld} 天前的旧任务和文件"
            );
        }

        /// <summary>
        /// 应用自定义转换设置
        /// </summary>
        private void ApplyCustomSettings(ConversionPreset preset, StartConversionRequest request)
        {
            // 基本设置
            if (!string.IsNullOrEmpty(request.OutputFormat))
                preset.OutputFormat = request.OutputFormat;

            // 分辨率设置
            if (!string.IsNullOrEmpty(request.Resolution))
            {
                if (request.Resolution == "custom" && request.CustomWidth.HasValue && request.CustomHeight.HasValue)
                {
                    preset.Resolution = $"{request.CustomWidth}x{request.CustomHeight}";
                }
                else if (request.Resolution != "custom")
                {
                    preset.Resolution = request.Resolution;
                }
            }

            // 视频设置
            if (!string.IsNullOrEmpty(request.VideoCodec))
                preset.VideoCodec = request.VideoCodec;

            if (!string.IsNullOrEmpty(request.FrameRate))
                preset.FrameRate = request.FrameRate;

            // 视频质量设置
            if (request.QualityMode == "crf" && !string.IsNullOrEmpty(request.VideoQuality))
            {
                preset.VideoQuality = request.VideoQuality;
            }
            else if (request.QualityMode == "bitrate" && request.VideoBitrate.HasValue)
            {
                preset.VideoQuality = $"{request.VideoBitrate}k";
            }

            // 音频设置
            if (!string.IsNullOrEmpty(request.AudioCodec))
                preset.AudioCodec = request.AudioCodec;

            // 音频质量设置
            if (request.AudioQualityMode == "bitrate")
            {
                if (!string.IsNullOrEmpty(request.AudioBitrate))
                {
                    if (request.AudioBitrate == "custom" && request.CustomAudioBitrateValue.HasValue)
                    {
                        preset.AudioQuality = $"{request.CustomAudioBitrateValue}k";
                    }
                    else if (request.AudioBitrate != "custom")
                    {
                        preset.AudioQuality = request.AudioBitrate;
                    }
                }
            }
            else if (request.AudioQualityMode == "quality" && request.AudioQualityValue.HasValue)
            {
                preset.AudioQuality = request.AudioQualityValue.ToString();
            }
        }

        /// <summary>
        /// 获取支持的格式列表
        /// </summary>
        [HttpGet("formats")]
        public async Task<IActionResult> GetSupportedFormats()
        {
            try
            {
                var inputFormats = await _formatDetectionService.GetSupportedInputFormatsAsync();
                var outputFormats = await _formatDetectionService.GetSupportedOutputFormatsAsync();
                var extendedSupport = await _formatDetectionService.GetExtendedFormatSupportAsync();

                var result = new
                {
                    InputFormats = inputFormats,
                    OutputFormats = outputFormats,
                    ExtendedSupport = extendedSupport,
                    SupportedExtensions = new[]
                    {
                        "mp4", "avi", "mov", "mkv", "wmv", "flv", "webm", "m4v", "3gp",
                        "mpg", "mpeg", "ts", "mts", "m2ts", "vob", "asf", "rm", "rmvb"
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取支持格式列表失败");
                return StatusCode(500, new { message = "获取支持格式列表失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 检查格式转换支持
        /// </summary>
        [HttpGet("formats/check")]
        public async Task<IActionResult> CheckFormatConversion([FromQuery] string inputFormat, [FromQuery] string outputFormat)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(inputFormat) || string.IsNullOrWhiteSpace(outputFormat))
                {
                    return BadRequest(new { message = "输入格式和输出格式不能为空" });
                }

                var isSupported = await _formatDetectionService.IsFormatConversionSupportedAsync(inputFormat, outputFormat);
                var formatInfo = await _formatDetectionService.GetFormatInfoAsync(outputFormat);

                var result = new
                {
                    InputFormat = inputFormat,
                    OutputFormat = outputFormat,
                    IsSupported = isSupported,
                    FormatInfo = formatInfo,
                    Recommendation = GetFormatRecommendation(inputFormat, outputFormat)
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查格式转换支持失败: {InputFormat} -> {OutputFormat}", inputFormat, outputFormat);
                return StatusCode(500, new { message = "检查格式转换支持失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 获取格式转换建议
        /// </summary>
        private object GetFormatRecommendation(string inputFormat, string outputFormat)
        {
            var recommendations = new List<string>();

            // 相同格式转换
            if (string.Equals(inputFormat, outputFormat, StringComparison.OrdinalIgnoreCase))
            {
                recommendations.Add("相同格式转换，适用于重新编码、压缩或修复文件");
            }

            // 格式特定建议
            var formatAdvice = outputFormat.ToLowerInvariant() switch
            {
                "mp4" => "MP4格式具有最佳兼容性，推荐用于通用播放",
                "mkv" => "MKV格式支持多轨道，适合高质量存储",
                "webm" => "WebM格式适合网页播放，文件较小",
                "avi" => "AVI格式兼容性好，但文件较大",
                _ => "标准格式转换"
            };

            recommendations.Add(formatAdvice);

            return new
            {
                Recommendations = recommendations,
                QualityNote = "建议使用CRF质量模式以获得最佳质量/大小平衡",
                PerformanceNote = GetPerformanceNote(inputFormat, outputFormat)
            };
        }

        /// <summary>
        /// 获取性能提示
        /// </summary>
        private string GetPerformanceNote(string inputFormat, string outputFormat)
        {
            // 容器转换（无需重新编码）
            var containerOnlyFormats = new[] { "mp4", "mkv", "mov" };
            if (containerOnlyFormats.Contains(inputFormat.ToLowerInvariant()) &&
                containerOnlyFormats.Contains(outputFormat.ToLowerInvariant()))
            {
                return "容器格式转换，速度较快";
            }

            // 特殊格式处理
            if (inputFormat.ToLowerInvariant() is "rm" or "rmvb")
            {
                return "专有格式解码，转换速度较慢";
            }

            if (inputFormat.ToLowerInvariant() is "vob")
            {
                return "DVD格式处理，可能需要额外时间";
            }

            return "标准转换速度";
        }
    }

    /// <summary>
    /// 开始转换请求模型
    /// </summary>
    public class StartConversionRequest
    {
        [Required(ErrorMessage = "请选择要转换的视频文件")]
        public IFormFile VideoFile { get; set; } = null!;
        public string? TaskName { get; set; }
        public string Preset { get; set; } = string.Empty;

        // 基本设置
        public string? OutputFormat { get; set; }
        public string? Resolution { get; set; }
        public int? CustomWidth { get; set; }
        public int? CustomHeight { get; set; }
        public string? AspectRatio { get; set; }

        // 视频设置
        public string? VideoCodec { get; set; }
        public string? FrameRate { get; set; }
        public string? QualityMode { get; set; } = "crf";
        public string? VideoQuality { get; set; }
        public int? VideoBitrate { get; set; }
        public string? EncodingPreset { get; set; }
        public string? Profile { get; set; }

        // 音频设置
        public string? AudioCodec { get; set; }
        public string? AudioChannels { get; set; }
        public string? AudioQualityMode { get; set; } = "bitrate";
        public string? AudioQuality { get; set; }
        public string? AudioBitrate { get; set; }
        public int? CustomAudioBitrateValue { get; set; }
        public int? AudioQualityValue { get; set; }
        public string? SampleRate { get; set; }
        public string? AudioVolume { get; set; }

        // 高级选项
        public string? StartTime { get; set; }
        public double? EndTime { get; set; }
        public string? Duration { get; set; }
        public bool Deinterlace { get; set; } = false;
        public string? Denoise { get; set; }
        public string? ColorSpace { get; set; }
        public string? PixelFormat { get; set; }
        public string? CustomParams { get; set; }
        public string? CustomParameters { get; set; }
        public string? HardwareAcceleration { get; set; }
        public string? VideoFilters { get; set; }
        public string? AudioFilters { get; set; }

        // 任务设置
        public int Priority { get; set; } = 0;
        public string? Tags { get; set; }
        public string? Notes { get; set; }

        public bool TwoPass { get; set; } = false;
        public bool FastStart { get; set; } = true;
        public bool CopyTimestamps { get; set; } = true;
    }

    /// <summary>
    /// 从上传文件开始转换请求模型
    /// </summary>
    public class StartConversionFromUploadRequest
    {
        [Required(ErrorMessage = "上传文件路径不能为空")]
        public string UploadedFilePath { get; set; } = string.Empty;
        public string? OriginalFileName { get; set; }
        public string? TaskName { get; set; }
        public string Preset { get; set; } = string.Empty;

        // 基本设置
        public string? OutputFormat { get; set; }
        public string? VideoCodec { get; set; }
        public string? AudioCodec { get; set; }
        public string? VideoQuality { get; set; }
        public string? AudioQuality { get; set; }
        public string? Resolution { get; set; }
        public string? FrameRate { get; set; }

        // 高级设置
        public string? QualityMode { get; set; }
        public string? AudioQualityMode { get; set; }
        public string? CustomParameters { get; set; }
        public string? HardwareAcceleration { get; set; }
        public string? VideoFilters { get; set; }
        public string? AudioFilters { get; set; }

        // 时间范围
        public double? StartTime { get; set; }
        public double? EndTime { get; set; }

        // 任务设置
        public int Priority { get; set; } = 0;
        public string? Tags { get; set; }
        public string? Notes { get; set; }

        // 编码设置
        public string? Profile { get; set; }
        public string? AudioChannels { get; set; }
        public string? SampleRate { get; set; }
        public string? AudioVolume { get; set; }
        public string? Denoise { get; set; }
        public string? ColorSpace { get; set; }
        public string? PixelFormat { get; set; }
        public bool TwoPass { get; set; } = false;
        public bool FastStart { get; set; } = true;
        public bool CopyTimestamps { get; set; } = true;
        public bool Deinterlace { get; set; } = false;
    }

    /// <summary>
    /// 已上传文件的IFormFile实现
    /// </summary>
    public class UploadedFormFile : IFormFile
    {
        private readonly string _filePath;
        private readonly string _fileName;
        private readonly long _length;

        public UploadedFormFile(string filePath, string fileName, long length)
        {
            _filePath = filePath;
            _fileName = fileName;
            _length = length;
        }

        public string ContentType => "application/octet-stream";
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{_fileName}\"";
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length => _length;
        public string Name => "file";
        public string FileName => _fileName;

        public void CopyTo(Stream target)
        {
            using var source = File.OpenRead(_filePath);
            source.CopyTo(target);
        }

        public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            using var source = File.OpenRead(_filePath);
            await source.CopyToAsync(target, cancellationToken);
        }

        public Stream OpenReadStream()
        {
            return File.OpenRead(_filePath);
        }
    }
}
