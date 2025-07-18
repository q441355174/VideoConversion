using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VideoConversion.Models;
using VideoConversion.Services;
using VideoConversion.Hubs;
using System.ComponentModel.DataAnnotations;

namespace VideoConversion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConversionController : ControllerBase
    {
        private readonly DatabaseService _databaseService;
        private readonly FileService _fileService;
        private readonly VideoConversionService _conversionService;
        private readonly LoggingService _loggingService;
        private readonly ILogger<ConversionController> _logger;
        private readonly IHubContext<ConversionHub> _hubContext;

        public ConversionController(
            DatabaseService databaseService,
            FileService fileService,
            VideoConversionService conversionService,
            LoggingService loggingService,
            ILogger<ConversionController> logger,
            IHubContext<ConversionHub> hubContext)
        {
            _databaseService = databaseService;
            _fileService = fileService;
            _conversionService = conversionService;
            _loggingService = loggingService;
            _logger = logger;
            _hubContext = hubContext;
        }

        /// <summary>
        /// 开始转换任务
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartConversion([FromForm] StartConversionRequest request)
        {
            return await ProcessConversionRequest(request, request.VideoFile);
        }

        /// <summary>
        /// 从已上传文件开始转换任务
        /// </summary>
        [HttpPost("start-from-upload")]
        public async Task<IActionResult> StartConversionFromUpload([FromForm] StartConversionFromUploadRequest request)
        {
            try
            {
                // 验证上传文件路径
                if (string.IsNullOrEmpty(request.UploadedFilePath) || !System.IO.File.Exists(request.UploadedFilePath))
                {
                    return BadRequest(new { success = false, message = "上传文件不存在" });
                }

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
                _logger.LogError(ex, "从上传文件创建转换任务失败");
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

        /// <summary>
        /// 获取任务状态
        /// </summary>
        [HttpGet("status/{taskId}")]
        public async Task<IActionResult> GetTaskStatus(string taskId)
        {
            try
            {
                var task = await _databaseService.GetTaskAsync(taskId);
                if (task == null)
                {
                    return NotFound(new { success = false, message = "任务不存在" });
                }

                return Ok(new
                {
                    success = true,
                    task = new
                    {
                        id = task.Id,
                        taskName = task.TaskName,
                        status = task.Status.ToString(),
                        progress = task.Progress,
                        errorMessage = task.ErrorMessage,
                        createdAt = task.CreatedAt,
                        startedAt = task.StartedAt,
                        completedAt = task.CompletedAt,
                        estimatedTimeRemaining = task.EstimatedTimeRemaining,
                        conversionSpeed = task.ConversionSpeed,
                        duration = task.Duration,
                        currentTime = task.CurrentTime,
                        originalFileName = task.OriginalFileName,
                        outputFileName = task.OutputFileName,
                        inputFormat = task.InputFormat,
                        outputFormat = task.OutputFormat,
                        videoCodec = task.VideoCodec,
                        audioCodec = task.AudioCodec
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务状态失败: {TaskId}", taskId);
                return StatusCode(500, new { success = false, message = "服务器内部错误" });
            }
        }

        /// <summary>
        /// 获取最近的任务
        /// </summary>
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentTasks([FromQuery] int count = 10)
        {
            try
            {
                var tasks = await _databaseService.GetAllTasksAsync(1, count);
                var result = tasks.Select(t => new
                {
                    t.Id,
                    t.TaskName,
                    t.Status,
                    t.Progress,
                    t.CreatedAt,
                    t.CompletedAt,
                    t.OriginalFileName,
                    t.OutputFileName
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取最近任务失败");
                return StatusCode(500, new { success = false, message = "服务器内部错误" });
            }
        }

        /// <summary>
        /// 下载转换后的文件
        /// </summary>
        [HttpGet("download/{taskId}")]
        public async Task<IActionResult> DownloadFile(string taskId)
        {
            try
            {
                var task = await _databaseService.GetTaskAsync(taskId);
                if (task == null)
                {
                    return NotFound("任务不存在");
                }

                if (task.Status != ConversionStatus.Completed || string.IsNullOrEmpty(task.OutputFilePath))
                {
                    return BadRequest("文件尚未准备好下载");
                }

                var downloadResult = await _fileService.GetFileDownloadStreamAsync(task.OutputFilePath);
                if (downloadResult.Stream == null)
                {
                    return NotFound("文件不存在");
                }

                return File(downloadResult.Stream, downloadResult.ContentType, downloadResult.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下载文件失败: {TaskId}", taskId);
                return StatusCode(500, "下载失败");
            }
        }

        /// <summary>
        /// 取消转换任务
        /// </summary>
        [HttpPost("cancel/{taskId}")]
        public async Task<IActionResult> CancelTask(string taskId)
        {
            try
            {
                _logger.LogInformation("收到取消任务请求: {TaskId}", taskId);
                await _conversionService.CancelConversionAsync(taskId);
                return Ok(new { success = true, message = "任务已取消" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消任务失败: {TaskId}", taskId);
                return StatusCode(500, new { success = false, message = "服务器内部错误" });
            }
        }

        /// <summary>
        /// 获取正在运行的进程信息
        /// </summary>
        [HttpGet("processes")]
        public IActionResult GetRunningProcesses()
        {
            try
            {
                var statistics = _conversionService.GetProcessStatistics();
                return Ok(new { success = true, data = statistics });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取进程信息失败");
                return StatusCode(500, new { success = false, message = "获取进程信息失败: " + ex.Message });
            }
        }

        /// <summary>
        /// 检查任务是否正在运行
        /// </summary>
        [HttpGet("is-running/{taskId}")]
        public IActionResult IsTaskRunning(string taskId)
        {
            try
            {
                var isRunning = _conversionService.IsTaskRunning(taskId);
                return Ok(new { success = true, isRunning = isRunning, taskId = taskId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查任务运行状态失败: {TaskId}", taskId);
                return StatusCode(500, new { success = false, message = "检查任务状态失败: " + ex.Message });
            }
        }

        /// <summary>
        /// 获取任务详细信息（包括编码器设置）
        /// </summary>
        [HttpGet("task-details/{taskId}")]
        public async Task<IActionResult> GetTaskDetails(string taskId)
        {
            try
            {
                var task = await _databaseService.GetTaskAsync(taskId);
                if (task == null)
                {
                    return NotFound(new { success = false, message = "任务不存在" });
                }

                return Ok(new
                {
                    success = true,
                    data = new
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
                        task.ErrorMessage
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务详细信息失败: {TaskId}", taskId);
                return StatusCode(500, new { success = false, message = "获取任务详细信息失败: " + ex.Message });
            }
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        [HttpDelete("{taskId}")]
        public async Task<IActionResult> DeleteTask(string taskId)
        {
            try
            {
                var task = await _databaseService.GetTaskAsync(taskId);
                if (task == null)
                {
                    return NotFound(new { success = false, message = "任务不存在" });
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

                return Ok(new { success = true, message = "任务已删除" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除任务失败: {TaskId}", taskId);
                return StatusCode(500, new { success = false, message = "服务器内部错误" });
            }
        }

        /// <summary>
        /// 获取任务列表（支持分页和筛选）
        /// </summary>
        [HttpGet("tasks")]
        public async Task<IActionResult> GetTasks(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
            [FromQuery] string? search = null)
        {
            try
            {
                _logger.LogInformation("获取任务列表: page={Page}, pageSize={PageSize}, status={Status}, search={Search}",
                    page, pageSize, status, search);

                var tasks = await _databaseService.GetAllTasksAsync(page, pageSize);
                _logger.LogInformation("从数据库获取到 {Count} 个任务", tasks?.Count ?? 0);

                // 确保 tasks 不为 null
                tasks = tasks ?? new List<ConversionTask>();

                // 应用状态筛选
                if (!string.IsNullOrEmpty(status) && Enum.TryParse<ConversionStatus>(status, out var statusEnum))
                {
                    tasks = tasks.Where(t => t.Status == statusEnum).ToList();
                    _logger.LogInformation("状态筛选后剩余 {Count} 个任务", tasks.Count);
                }

                // 应用搜索筛选
                if (!string.IsNullOrEmpty(search))
                {
                    tasks = tasks.Where(t =>
                        (t.TaskName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (t.OriginalFileName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                    ).ToList();
                    _logger.LogInformation("搜索筛选后剩余 {Count} 个任务", tasks.Count);
                }

                // 计算总页数（简化版本，实际应该在数据库层面处理）
                var totalTasks = tasks.Count;
                var totalPages = totalTasks > 0 ? (int)Math.Ceiling((double)totalTasks / pageSize) : 1;

                var result = new
                {
                    success = true,
                    tasks = tasks.Select(t => new
                    {
                        t.Id,
                        TaskName = t.TaskName ?? "",
                        Status = t.Status.ToString(),
                        t.Progress,
                        t.CreatedAt,
                        t.StartedAt,
                        t.CompletedAt,
                        OriginalFileName = t.OriginalFileName ?? "",
                        OutputFileName = t.OutputFileName ?? "",
                        t.OriginalFileSize,
                        t.OutputFileSize,
                        InputFormat = t.InputFormat ?? "",
                        OutputFormat = t.OutputFormat ?? "",
                        VideoCodec = t.VideoCodec ?? "",
                        AudioCodec = t.AudioCodec ?? "",
                        ErrorMessage = t.ErrorMessage ?? ""
                    }).ToList(),
                    totalPages,
                    currentPage = page,
                    totalTasks
                };

                _logger.LogInformation("返回任务列表: {TaskCount} 个任务, {TotalPages} 页", tasks.Count, totalPages);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务列表失败");
                return StatusCode(500, new { success = false, message = "服务器内部错误: " + ex.Message });
            }
        }

        /// <summary>
        /// 清理旧任务
        /// </summary>
        [HttpPost("cleanup")]
        public async Task<IActionResult> CleanupOldTasks([FromQuery] int daysOld = 30)
        {
            try
            {
                var deletedCount = await _databaseService.CleanupOldTasksAsync(daysOld);
                await _fileService.CleanupOldFilesAsync(daysOld);

                return Ok(new { success = true, deletedCount, message = $"清理了 {deletedCount} 个旧任务" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理旧任务失败");
                return StatusCode(500, new { success = false, message = "清理失败" });
            }
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
