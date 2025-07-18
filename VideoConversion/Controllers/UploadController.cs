using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VideoConversion.Hubs;
using VideoConversion.Services;
using System.Collections.Concurrent;

namespace VideoConversion.Controllers
{
    /// <summary>
    /// 文件上传控制器 - 支持大文件上传和进度跟踪
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly ILogger<UploadController> _logger;
        private readonly FileService _fileService;
        private readonly ConversionTaskService _conversionTaskService;
        private readonly IHubContext<ConversionHub> _hubContext;
        private static readonly ConcurrentDictionary<string, UploadProgress> _uploadProgress = new();

        public UploadController(
            ILogger<UploadController> logger,
            FileService fileService,
            ConversionTaskService conversionTaskService,
            IHubContext<ConversionHub> hubContext)
        {
            _logger = logger;
            _fileService = fileService;
            _conversionTaskService = conversionTaskService;
            _hubContext = hubContext;
        }

        /// <summary>
        /// 大文件上传并创建转换任务接口 - 支持进度跟踪
        /// </summary>
        [HttpPost("large-file")]
        [RequestSizeLimit(32212254720)] // 30GB
        [RequestFormLimits(MultipartBodyLengthLimit = 32212254720)]
        public async Task<IActionResult> UploadLargeFileAndCreateTask()
        {
            var uploadId = Guid.NewGuid().ToString();
            
            try
            {
                var form = await Request.ReadFormAsync();
                var file = form.Files.FirstOrDefault();
                
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, message = "请选择一个文件" });
                }

                _logger.LogInformation("开始大文件上传: {FileName} ({FileSize} bytes)", file.FileName, file.Length);

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

                    _logger.LogInformation("大文件上传完成: {FileName} -> {FilePath}", file.FileName, result.FilePath);

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
                        _logger.LogError("上传成功但创建转换任务失败: {Error}", conversionResult.ErrorMessage);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "大文件上传失败: {UploadId}", uploadId);
                
                if (_uploadProgress.TryGetValue(uploadId, out var progress))
                {
                    progress.Status = "failed";
                    progress.ErrorMessage = ex.Message;
                }

                await _hubContext.Clients.All.SendAsync("UploadFailed", new
                {
                    UploadId = uploadId,
                    ErrorMessage = ex.Message
                });

                return StatusCode(500, new { success = false, message = "上传过程中发生错误" });
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
        /// 获取上传进度
        /// </summary>
        [HttpGet("progress/{uploadId}")]
        public IActionResult GetUploadProgress(string uploadId)
        {
            if (_uploadProgress.TryGetValue(uploadId, out var progress))
            {
                var progressPercent = progress.TotalSize > 0 ? (int)((progress.UploadedSize * 100) / progress.TotalSize) : 0;
                var elapsed = DateTime.Now - progress.StartTime;
                var speed = elapsed.TotalSeconds > 0 ? progress.UploadedSize / elapsed.TotalSeconds : 0;
                var remainingBytes = progress.TotalSize - progress.UploadedSize;
                var estimatedTimeRemaining = speed > 0 ? (int)(remainingBytes / speed) : 0;

                return Ok(new
                {
                    uploadId = progress.UploadId,
                    fileName = progress.FileName,
                    totalSize = progress.TotalSize,
                    uploadedSize = progress.UploadedSize,
                    progressPercent = progressPercent,
                    speed = speed,
                    estimatedTimeRemaining = estimatedTimeRemaining,
                    status = progress.Status,
                    errorMessage = progress.ErrorMessage,
                    startTime = progress.StartTime,
                    completedAt = progress.CompletedAt
                });
            }

            return NotFound(new { success = false, message = "上传进度不存在" });
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

                // 创建转换任务请求对象
                var request = new ConversionTaskRequest
                {
                    TaskName = form["taskName"].FirstOrDefault(),
                    Preset = form["preset"].FirstOrDefault() ?? "default",
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
                _logger.LogError(ex, "从上传文件创建转换任务失败: {FilePath}", filePath);
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
        public long TotalSize { get; set; }
        public long UploadedSize { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = string.Empty; // uploading, completed, failed
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
