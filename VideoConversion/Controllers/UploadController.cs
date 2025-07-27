using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VideoConversion.Hubs;
using VideoConversion.Services;
using VideoConversion.Controllers.Base;
using System.Collections.Concurrent;

namespace VideoConversion.Controllers
{
    /// <summary>
    /// 文件上传控制器 - 支持大文件上传和进度跟踪
    /// 已优化使用 BaseApiController
    /// </summary>
    [Route("api/[controller]")]
    public class UploadController : BaseApiController
    {
        private readonly FileService _fileService;
        private readonly ConversionTaskService _conversionTaskService;
        private readonly IHubContext<ConversionHub> _hubContext;
        private static readonly ConcurrentDictionary<string, UploadProgress> _uploadProgress = new();
        public UploadController(
            ILogger<UploadController> logger,
            FileService fileService,
            ConversionTaskService conversionTaskService,
            IHubContext<ConversionHub> hubContext) : base(logger)
        {
            _fileService = fileService;
            _conversionTaskService = conversionTaskService;
            _hubContext = hubContext;
        }

        /// <summary>
        /// 统一文件上传接口 - 支持所有文件大小，自动优化处理
        /// </summary>
        [HttpPost("unified")]
        [RequestSizeLimit(32212254720)] // 30GB
        [RequestFormLimits(MultipartBodyLengthLimit = 32212254720)]
        public async Task<IActionResult> UploadUnifiedFileAndCreateTask()
        {
            var uploadId = Guid.NewGuid().ToString();
            IFormCollection? form = null;

            try
            {
                Logger.LogInformation("开始处理统一文件上传请求: UploadId={UploadId}, ClientIP={ClientIP}",
                    uploadId, GetClientIpAddress());

                // 设置更长的超时时间用于读取表单数据
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
                form = await Request.ReadFormAsync(cts.Token);

                var file = form.Files.FirstOrDefault();
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, message = "未找到有效的文件" });
                }

                // 获取重试信息
                var retryAttempt = int.TryParse(form["RetryAttempt"], out var attempt) ? attempt : 1;
                var maxRetries = int.TryParse(form["MaxRetries"], out var max) ? max : 1;

                Logger.LogInformation("文件上传信息: FileName={FileName}, Size={Size}, Attempt={Attempt}/{MaxRetries}",
                    file.FileName, file.Length, retryAttempt, maxRetries);

                // 初始化上传进度
                var progress = new UploadProgress
                {
                    UploadId = uploadId,
                    FileName = file.FileName,
                    TotalBytes = file.Length,
                    BytesUploaded = 0,
                    Status = "uploading",
                    StartedAt = DateTime.Now,
                    RetryAttempt = retryAttempt,
                    MaxRetries = maxRetries
                };
                _uploadProgress[uploadId] = progress;

                // 通知客户端上传开始
                await _hubContext.Clients.All.SendAsync("UploadStarted", new
                {
                    UploadId = uploadId,
                    FileName = file.FileName,
                    TotalBytes = file.Length,
                    RetryAttempt = retryAttempt,
                    Timestamp = DateTime.Now
                });

                // 保存文件
                var saveResult = await _fileService.SaveUploadedFileAsync(file, uploadId);

                if (!saveResult.Success)
                {
                    return BadRequest(new { success = false, message = saveResult.ErrorMessage });
                }

                // 更新进度为文件保存完成
                progress.BytesUploaded = file.Length;
                progress.Status = "processing";

                await _hubContext.Clients.All.SendAsync("UploadProgress", new
                {
                    UploadId = uploadId,
                    BytesUploaded = file.Length,
                    TotalBytes = file.Length,
                    Percentage = 100.0,
                    Status = "processing",
                    Timestamp = DateTime.Now
                });

                // 创建转换任务
                var taskResult = await CreateConversionTaskFromUpload(file, saveResult.FilePath, form);

                if (taskResult.Success)
                {
                    progress.Status = "completed";
                    progress.CompletedAt = DateTime.Now;
                    progress.TaskId = taskResult.TaskId;

                    await _hubContext.Clients.All.SendAsync("UploadCompleted", new
                    {
                        UploadId = uploadId,
                        TaskId = taskResult.TaskId,
                        FileName = file.FileName,
                        Timestamp = DateTime.Now
                    });

                    Logger.LogInformation("统一文件上传成功: UploadId={UploadId}, TaskId={TaskId}, FileName={FileName}",
                        uploadId, taskResult.TaskId, file.FileName);

                    return Ok(new
                    {
                        success = true,
                        taskId = taskResult.TaskId,
                        taskName = taskResult.TaskName,
                        message = "文件上传并创建转换任务成功"
                    });
                }
                else
                {
                    progress.Status = "failed";
                    progress.ErrorMessage = taskResult.ErrorMessage;
                    progress.CompletedAt = DateTime.Now;

                    return BadRequest(new { success = false, message = taskResult.ErrorMessage });
                }
            }
            catch (OperationCanceledException ex)
            {
                Logger.LogWarning(ex, "统一文件上传被取消: UploadId={UploadId}, FileName={FileName}, ClientIP={ClientIP}",
                    uploadId, form?.Files?.FirstOrDefault()?.FileName ?? "Unknown", GetClientIpAddress());

                if (_uploadProgress.TryGetValue(uploadId, out var progress))
                {
                    progress.Status = "cancelled";
                    progress.ErrorMessage = "上传被取消";
                    progress.CompletedAt = DateTime.Now;
                }

                await _hubContext.Clients.All.SendAsync("UploadFailed", new
                {
                    UploadId = uploadId,
                    ErrorMessage = "上传被取消",
                    ErrorType = "Cancelled",
                    Timestamp = DateTime.Now
                });

                return BadRequest(new { success = false, message = "上传被取消" });
            }
            catch (Microsoft.AspNetCore.Http.BadHttpRequestException ex) when (ex.Message.Contains("Unexpected end of request content"))
            {
                Logger.LogWarning(ex, "统一文件上传连接中断: UploadId={UploadId}, FileName={FileName}, ClientIP={ClientIP}",
                    uploadId, form?.Files?.FirstOrDefault()?.FileName ?? "Unknown", GetClientIpAddress());

                if (_uploadProgress.TryGetValue(uploadId, out var progress))
                {
                    progress.Status = "failed";
                    progress.ErrorMessage = "网络连接中断，请检查网络连接后重试";
                    progress.CompletedAt = DateTime.Now;
                }

                await _hubContext.Clients.All.SendAsync("UploadFailed", new
                {
                    UploadId = uploadId,
                    ErrorMessage = "网络连接中断，请检查网络连接后重试",
                    ErrorType = "NetworkInterruption",
                    CanRetry = true,
                    Timestamp = DateTime.Now
                });

                return BadRequest(new {
                    success = false,
                    message = "网络连接中断，请检查网络连接后重试",
                    errorType = "NetworkInterruption",
                    canRetry = true
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "统一文件上传失败: UploadId={UploadId}, FileName={FileName}, ClientIP={ClientIP}",
                    uploadId, form?.Files?.FirstOrDefault()?.FileName ?? "Unknown", GetClientIpAddress());

                if (_uploadProgress.TryGetValue(uploadId, out var progress))
                {
                    progress.Status = "failed";
                    progress.ErrorMessage = ex.Message;
                    progress.CompletedAt = DateTime.Now;
                }

                await _hubContext.Clients.All.SendAsync("UploadFailed", new
                {
                    UploadId = uploadId,
                    ErrorMessage = ex.Message,
                    ErrorType = "General",
                    Timestamp = DateTime.Now
                });

                return ServerError("上传过程中发生错误");
            }
        }

        /// <summary>
        /// 大文件上传并创建转换任务接口 - 已优化（保持原有逻辑，增强验证和日志）
        /// 支持进度跟踪和 SignalR 实时通知
        /// </summary>
        [HttpPost("large-file")]
        [RequestSizeLimit(32212254720)] // 30GB
        [RequestFormLimits(MultipartBodyLengthLimit = 32212254720)]
        public async Task<IActionResult> UploadLargeFileAndCreateTask()
        {
            var uploadId = Guid.NewGuid().ToString();
            IFormCollection? form = null;

            try
            {
                Logger.LogInformation("开始处理大文件上传请求: UploadId={UploadId}, ClientIP={ClientIP}",
                    uploadId, GetClientIpAddress());

                // 设置更长的超时时间用于读取表单数据
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
                form = await Request.ReadFormAsync(cts.Token);
                var file = form.Files.FirstOrDefault();

                // 记录表单数据用于调试
                Logger.LogInformation("收到表单数据: 文件数={FileCount}, 字段数={FieldCount}",
                    form.Files.Count, form.Count);

                foreach (var key in form.Keys)
                {
                    Logger.LogDebug("表单字段: {Key} = {Value}", key, form[key]);
                }

                // 使用基类的验证方法
                if (file == null || file.Length == 0)
                {
                    Logger.LogWarning("上传请求无效: 未选择文件或文件为空, UploadId={UploadId}", uploadId);
                    return ValidationError("请选择一个有效的文件");
                }

                // 文件大小验证
                if (file.Length > 32212254720) // 30GB
                {
                    Logger.LogWarning("上传文件过大: {FileSize} bytes, UploadId={UploadId}", file.Length, uploadId);
                    return ValidationError("文件大小不能超过30GB");
                }

                Logger.LogInformation("开始大文件上传: {FileName} ({FileSize} bytes), UploadId={UploadId}",
                    file.FileName, file.Length, uploadId);

                // 初始化上传进度
                var progress = new UploadProgress
                {
                    UploadId = uploadId,
                    FileName = file.FileName,
                    TotalSize = file.Length,
                    UploadedSize = 0,
                    StartTime = DateTime.Now,
                    Status = "uploading"
                };
                _uploadProgress[uploadId] = progress;

                // 通知客户端开始上传
                await _hubContext.Clients.All.SendAsync("UploadStarted", new
                {
                    UploadId = uploadId,
                    FileName = file.FileName,
                    TotalSize = file.Length
                });

                // 保存文件并跟踪进度
                var result = await _fileService.SaveUploadedFileWithProgressAsync(file, uploadId, UpdateUploadProgress);

                if (result.Success)
                {
                    progress.Status = "completed";
                    progress.CompletedAt = DateTime.Now;

                    await _hubContext.Clients.All.SendAsync("UploadCompleted", new
                    {
                        UploadId = uploadId,
                        FilePath = result.FilePath,
                        Duration = (DateTime.Now - progress.StartTime).TotalSeconds
                    });

                    Logger.LogInformation("大文件上传完成: {FileName} -> {FilePath}, UploadId={UploadId}",
                        file.FileName, result.FilePath, uploadId);

                    // 现在创建转换任务 - 这是关键的修复！
                    var conversionResult = await CreateConversionTaskFromUpload(file, result.FilePath, form);

                    if (conversionResult.Success)
                    {
                        return Ok(new
                        {
                            success = true,
                            uploadId = uploadId,
                            taskId = conversionResult.TaskId,
                            taskName = conversionResult.TaskName,
                            filePath = result.FilePath,
                            fileName = file.FileName,
                            fileSize = file.Length,
                            message = "文件上传并创建转换任务成功"
                        });
                    }
                    else
                    {
                        // 上传成功但创建任务失败
                        Logger.LogError("上传成功但创建转换任务失败: UploadId={UploadId}, Error={Error}",
                            uploadId, conversionResult.ErrorMessage);
                        return StatusCode(500, new
                        {
                            success = false,
                            message = $"文件上传成功，但创建转换任务失败: {conversionResult.ErrorMessage}",
                            uploadId = uploadId,
                            filePath = result.FilePath
                        });
                    }
                }
                else
                {
                    progress.Status = "failed";
                    progress.ErrorMessage = result.ErrorMessage;
                    
                    await _hubContext.Clients.All.SendAsync("UploadFailed", new
                    {
                        UploadId = uploadId,
                        ErrorMessage = result.ErrorMessage
                    });

                    return BadRequest(new { success = false, message = result.ErrorMessage });
                }
            }
            catch (OperationCanceledException ex)
            {
                Logger.LogWarning(ex, "大文件上传被取消: UploadId={UploadId}, FileName={FileName}, ClientIP={ClientIP}",
                    uploadId, form?.Files?.FirstOrDefault()?.FileName ?? "Unknown", GetClientIpAddress());

                if (_uploadProgress.TryGetValue(uploadId, out var progress))
                {
                    progress.Status = "cancelled";
                    progress.ErrorMessage = "上传被取消";
                    progress.CompletedAt = DateTime.Now;
                }

                await _hubContext.Clients.All.SendAsync("UploadFailed", new
                {
                    UploadId = uploadId,
                    ErrorMessage = "上传被取消",
                    ErrorType = "Cancelled",
                    Timestamp = DateTime.Now
                });

                return BadRequest(new { success = false, message = "上传被取消" });
            }
            catch (Microsoft.AspNetCore.Http.BadHttpRequestException ex) when (ex.Message.Contains("Unexpected end of request content"))
            {
                Logger.LogWarning(ex, "大文件上传连接中断: UploadId={UploadId}, FileName={FileName}, ClientIP={ClientIP}",
                    uploadId, form?.Files?.FirstOrDefault()?.FileName ?? "Unknown", GetClientIpAddress());

                if (_uploadProgress.TryGetValue(uploadId, out var progress))
                {
                    progress.Status = "failed";
                    progress.ErrorMessage = "网络连接中断，请检查网络连接后重试";
                    progress.CompletedAt = DateTime.Now;
                }

                await _hubContext.Clients.All.SendAsync("UploadFailed", new
                {
                    UploadId = uploadId,
                    ErrorMessage = "网络连接中断，请检查网络连接后重试",
                    ErrorType = "NetworkInterruption",
                    CanRetry = true,
                    Timestamp = DateTime.Now
                });

                return BadRequest(new {
                    success = false,
                    message = "网络连接中断，请检查网络连接后重试",
                    errorType = "NetworkInterruption",
                    canRetry = true
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "大文件上传失败: UploadId={UploadId}, FileName={FileName}, ClientIP={ClientIP}",
                    uploadId, form?.Files?.FirstOrDefault()?.FileName ?? "Unknown", GetClientIpAddress());

                if (_uploadProgress.TryGetValue(uploadId, out var progress))
                {
                    progress.Status = "failed";
                    progress.ErrorMessage = ex.Message;
                    progress.CompletedAt = DateTime.Now;
                }

                await _hubContext.Clients.All.SendAsync("UploadFailed", new
                {
                    UploadId = uploadId,
                    ErrorMessage = ex.Message,
                    ErrorType = "General",
                    Timestamp = DateTime.Now
                });

                return ServerError("上传过程中发生错误");
            }
            finally
            {
                // 清理进度信息（延迟清理，给客户端时间获取最终状态）
                _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
                {
                    _uploadProgress.TryRemove(uploadId, out var _);
                });
            }
        }

        /// <summary>
        /// 获取上传进度 - 已优化使用 BaseApiController
        /// </summary>
        [HttpGet("progress/{uploadId}")]
        public async Task<IActionResult> GetUploadProgress(string uploadId)
        {
            // 使用基类的参数验证
            if (string.IsNullOrWhiteSpace(uploadId))
                return ValidationError("上传ID不能为空");

            return await SafeExecuteAsync(
                async () =>
                {
                    await Task.CompletedTask; // 占位符，因为原方法是同步的

                    if (!_uploadProgress.TryGetValue(uploadId, out var progress))
                    {
                        throw new FileNotFoundException("上传进度不存在");
                    }

                    var progressPercent = progress.TotalSize > 0 ? (int)((progress.UploadedSize * 100) / progress.TotalSize) : 0;
                    var elapsed = DateTime.Now - progress.StartTime;
                    var speed = elapsed.TotalSeconds > 0 ? progress.UploadedSize / elapsed.TotalSeconds : 0;
                    var remainingBytes = progress.TotalSize - progress.UploadedSize;
                    var estimatedTimeRemaining = speed > 0 ? (int)(remainingBytes / speed) : 0;

                    return new
                    {
                        uploadId = progress.UploadId,
                        fileName = progress.FileName,
                        totalSize = progress.TotalSize,
                        uploadedSize = progress.UploadedSize,
                        progressPercent = progressPercent,
                        speed = Math.Round(speed / 1024 / 1024, 2), // MB/s
                        estimatedTimeRemaining = estimatedTimeRemaining,
                        status = progress.Status,
                        errorMessage = progress.ErrorMessage,
                        startTime = progress.StartTime,
                        completedAt = progress.CompletedAt,
                        // 添加更多有用信息
                        elapsedTime = elapsed.TotalSeconds,
                        speedFormatted = $"{Math.Round(speed / 1024 / 1024, 2)} MB/s"
                    };
                },
                "获取上传进度",
                "上传进度获取成功"
            );
        }

        /// <summary>
        /// 从上传文件创建转换任务
        /// </summary>
        private async Task<(bool Success, string TaskId, string TaskName, string ErrorMessage)> CreateConversionTaskFromUpload(
            IFormFile file, string filePath, IFormCollection form)
        {
            try
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                Logger.LogInformation("开始从上传文件创建转换任务: 文件={FileName}, 路径={FilePath}",
                    file.FileName, filePath);

                // 记录关键表单字段
                var preset = form["preset"].FirstOrDefault() ?? "default";
                Logger.LogInformation("使用预设: {Preset}", preset);

                // 创建转换任务请求对象
                var request = new ConversionTaskRequest
                {
                    TaskName = form["taskName"].FirstOrDefault(),
                    Preset = preset,
                    OutputFormat = form["outputFormat"].FirstOrDefault(),
                    VideoCodec = form["videoCodec"].FirstOrDefault(),
                    AudioCodec = form["audioCodec"].FirstOrDefault(),
                    VideoQuality = form["videoQuality"].FirstOrDefault(),
                    AudioQuality = form["audioQuality"].FirstOrDefault(),
                    Resolution = form["resolution"].FirstOrDefault(),
                    FrameRate = form["frameRate"].FirstOrDefault(),
                    QualityMode = form["qualityMode"].FirstOrDefault(),
                    AudioQualityMode = form["audioQualityMode"].FirstOrDefault(),
                    CustomParameters = form["customParams"].FirstOrDefault(),
                    HardwareAcceleration = form["hardwareAcceleration"].FirstOrDefault(),
                    VideoFilters = form["videoFilters"].FirstOrDefault(),
                    AudioFilters = form["audioFilters"].FirstOrDefault(),
                    Priority = int.TryParse(form["priority"].FirstOrDefault(), out var priority) ? priority : 0,
                    Tags = form["tags"].FirstOrDefault(),
                    Notes = form["notes"].FirstOrDefault(),
                    EncodingPreset = form["encodingPreset"].FirstOrDefault(),
                    Profile = form["profile"].FirstOrDefault(),
                    AudioChannels = form["audioChannels"].FirstOrDefault(),
                    SampleRate = form["sampleRate"].FirstOrDefault(),
                    AudioVolume = form["audioVolume"].FirstOrDefault(),
                    Denoise = form["denoise"].FirstOrDefault(),
                    ColorSpace = form["colorSpace"].FirstOrDefault(),
                    PixelFormat = form["pixelFormat"].FirstOrDefault(),
                    TwoPass = bool.TryParse(form["twoPass"].FirstOrDefault(), out var twoPass) && twoPass,
                    FastStart = !bool.TryParse(form["fastStart"].FirstOrDefault(), out var fastStart) || fastStart,
                    CopyTimestamps = !bool.TryParse(form["copyTimestamps"].FirstOrDefault(), out var copyTimestamps) || copyTimestamps,
                    Deinterlace = bool.TryParse(form["deinterlace"].FirstOrDefault(), out var deinterlace) && deinterlace
                };

                // 解析时间范围
                if (double.TryParse(form["startTime"].FirstOrDefault(), out var startTime))
                {
                    request.StartTime = startTime;
                }
                if (double.TryParse(form["endTime"].FirstOrDefault(), out var endTime))
                {
                    request.EndTime = endTime;
                }

                // 使用ConversionTaskService创建任务
                var result = await _conversionTaskService.CreateTaskFromUploadedFile(
                    filePath,
                    file.FileName,
                    file.Length,
                    request,
                    clientIp);

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "从上传文件创建转换任务失败: {FilePath}", filePath);
                return (false, "", "", $"创建转换任务异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新上传进度的回调方法 - 带节流控制
        /// </summary>
        private async Task UpdateUploadProgress(string uploadId, long uploadedSize)
        {
            if (_uploadProgress.TryGetValue(uploadId, out var progress))
            {
                progress.UploadedSize = uploadedSize;

                var progressPercent = progress.TotalSize > 0 ? (int)((uploadedSize * 100) / progress.TotalSize) : 0;
                var elapsed = DateTime.Now - progress.StartTime;
                var speed = elapsed.TotalSeconds > 0 ? uploadedSize / elapsed.TotalSeconds : 0;
                var remainingBytes = progress.TotalSize - uploadedSize;
                var estimatedTimeRemaining = speed > 0 ? (int)(remainingBytes / speed) : 0;

                // 通过SignalR实时推送进度（已经在FileService中进行了节流控制）
                await _hubContext.Clients.All.SendAsync("UploadProgress", new
                {
                    UploadId = uploadId,
                    FileName = progress.FileName,
                    TotalSize = progress.TotalSize,
                    UploadedSize = uploadedSize,
                    ProgressPercent = progressPercent,
                    Speed = speed,
                    EstimatedTimeRemaining = estimatedTimeRemaining,
                    SpeedFormatted = FormatSpeed(speed),
                    TimeRemainingFormatted = FormatTime(estimatedTimeRemaining)
                });
            }
        }

        /// <summary>
        /// 格式化速度显示
        /// </summary>
        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024) return $"{bytesPerSecond:F0} B/s";
            if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024:F1} KB/s";
            if (bytesPerSecond < 1024 * 1024 * 1024) return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
            return $"{bytesPerSecond / (1024 * 1024 * 1024):F1} GB/s";
        }

        /// <summary>
        /// 格式化时间显示
        /// </summary>
        private string FormatTime(int seconds)
        {
            if (seconds < 60) return $"{seconds}秒";
            if (seconds < 3600) return $"{seconds / 60}分{seconds % 60}秒";
            return $"{seconds / 3600}时{(seconds % 3600) / 60}分";
        }
    }

    /// <summary>
    /// 上传进度信息
    /// </summary>
    public class UploadProgress
    {
        public string UploadId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public long BytesUploaded { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = string.Empty; // uploading, processing, completed, failed, cancelled
        public string ErrorMessage { get; set; } = string.Empty;
        public string? TaskId { get; set; }
        public int RetryAttempt { get; set; } = 1;
        public int MaxRetries { get; set; } = 1;

        // 兼容性属性
        public long TotalSize
        {
            get => TotalBytes;
            set => TotalBytes = value;
        }

        public long UploadedSize
        {
            get => BytesUploaded;
            set => BytesUploaded = value;
        }

        public DateTime StartTime
        {
            get => StartedAt;
            set => StartedAt = value;
        }
    }
}
