using Microsoft.AspNetCore.Mvc;
using VideoConversion.Models;
using VideoConversion.Services;

namespace VideoConversion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConversionController : ControllerBase
    {
        private readonly DatabaseService _databaseService;
        private readonly FileService _fileService;
        private readonly VideoConversionService _conversionService;
        private readonly ILogger<ConversionController> _logger;

        public ConversionController(
            DatabaseService databaseService,
            FileService fileService,
            VideoConversionService conversionService,
            ILogger<ConversionController> logger)
        {
            _databaseService = databaseService;
            _fileService = fileService;
            _conversionService = conversionService;
            _logger = logger;
        }

        /// <summary>
        /// 开始转换任务
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartConversion([FromForm] StartConversionRequest request)
        {
            try
            {
                // 验证文件
                var validation = _fileService.ValidateFile(request.VideoFile);
                if (!validation.IsValid)
                {
                    return BadRequest(new { success = false, message = validation.ErrorMessage });
                }

                // 保存上传的文件
                var saveResult = await _fileService.SaveUploadedFileAsync(request.VideoFile);
                if (!saveResult.Success)
                {
                    return BadRequest(new { success = false, message = saveResult.ErrorMessage });
                }

                // 获取转换预设作为基础
                var preset = ConversionPreset.GetPresetByName(request.Preset) ?? ConversionPreset.GetDefaultPreset();

                // 应用详细的自定义设置
                ApplyCustomSettings(preset, request);

                // 生成输出文件路径
                var outputFilePath = _fileService.GenerateOutputFilePath(
                    request.VideoFile.FileName, 
                    preset.OutputFormat, 
                    request.TaskName);

                // 创建转换任务
                var task = new ConversionTask
                {
                    TaskName = !string.IsNullOrEmpty(request.TaskName) ? request.TaskName : Path.GetFileNameWithoutExtension(request.VideoFile.FileName),
                    OriginalFileName = request.VideoFile.FileName,
                    OriginalFilePath = saveResult.FilePath,
                    OutputFileName = Path.GetFileName(outputFilePath),
                    OutputFilePath = outputFilePath,
                    OriginalFileSize = request.VideoFile.Length,
                    InputFormat = Path.GetExtension(request.VideoFile.FileName).TrimStart('.'),
                    OutputFormat = preset.OutputFormat,
                    VideoCodec = preset.VideoCodec,
                    AudioCodec = preset.AudioCodec,
                    VideoQuality = preset.VideoQuality,
                    AudioQuality = preset.AudioQuality,
                    Resolution = preset.Resolution,
                    FrameRate = preset.FrameRate,
                    Status = ConversionStatus.Pending,

                    // 扩展参数
                    EncodingPreset = request.EncodingPreset,
                    Profile = request.Profile,
                    AudioChannels = request.AudioChannels,
                    SampleRate = request.SampleRate,
                    AudioVolume = request.AudioVolume,
                    StartTime = request.StartTime,
                    DurationLimit = request.Duration,
                    Deinterlace = request.Deinterlace,
                    Denoise = request.Denoise,
                    ColorSpace = request.ColorSpace,
                    PixelFormat = request.PixelFormat,
                    CustomParams = request.CustomParams,
                    TwoPass = request.TwoPass,
                    FastStart = request.FastStart,
                    CopyTimestamps = request.CopyTimestamps,
                    QualityMode = request.QualityMode,
                    AudioQualityMode = request.AudioQualityMode
                };

                // 保存任务到数据库
                await _databaseService.CreateTaskAsync(task);

                _logger.LogInformation("创建转换任务: {TaskId} - {TaskName}", task.Id, task.TaskName);

                return Ok(new { 
                    success = true, 
                    taskId = task.Id, 
                    taskName = task.TaskName,
                    message = "转换任务已创建，正在队列中等待处理" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建转换任务失败");
                return StatusCode(500, new { success = false, message = "服务器内部错误" });
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
                        task.Id,
                        task.TaskName,
                        task.Status,
                        task.Progress,
                        task.ErrorMessage,
                        task.CreatedAt,
                        task.StartedAt,
                        task.CompletedAt,
                        task.EstimatedTimeRemaining,
                        task.ConversionSpeed,
                        task.Duration,
                        task.CurrentTime
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
                var success = await _conversionService.CancelConversionAsync(taskId);
                if (success)
                {
                    return Ok(new { success = true, message = "任务已取消" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "无法取消任务" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消任务失败: {TaskId}", taskId);
                return StatusCode(500, new { success = false, message = "服务器内部错误" });
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
                var tasks = await _databaseService.GetAllTasksAsync(page, pageSize);

                // 应用状态筛选
                if (!string.IsNullOrEmpty(status) && Enum.TryParse<ConversionStatus>(status, out var statusEnum))
                {
                    tasks = tasks.Where(t => t.Status == statusEnum).ToList();
                }

                // 应用搜索筛选
                if (!string.IsNullOrEmpty(search))
                {
                    tasks = tasks.Where(t =>
                        t.TaskName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        t.OriginalFileName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                // 计算总页数（简化版本，实际应该在数据库层面处理）
                var totalTasks = tasks.Count;
                var totalPages = (int)Math.Ceiling((double)totalTasks / pageSize);

                var result = new
                {
                    tasks = tasks.Select(t => new
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
                        t.OriginalFileSize,
                        t.OutputFileSize,
                        t.InputFormat,
                        t.OutputFormat,
                        t.VideoCodec,
                        t.AudioCodec,
                        t.ErrorMessage
                    }).ToList(),
                    totalPages,
                    currentPage = page,
                    totalTasks
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务列表失败");
                return StatusCode(500, new { success = false, message = "服务器内部错误" });
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
        public string? AudioBitrate { get; set; }
        public int? CustomAudioBitrateValue { get; set; }
        public int? AudioQualityValue { get; set; }
        public string? SampleRate { get; set; }
        public int? AudioVolume { get; set; } = 100;

        // 高级选项
        public string? StartTime { get; set; }
        public string? Duration { get; set; }
        public string? Deinterlace { get; set; }
        public string? Denoise { get; set; }
        public string? ColorSpace { get; set; }
        public string? PixelFormat { get; set; }
        public string? CustomParams { get; set; }
        public bool TwoPass { get; set; } = false;
        public bool FastStart { get; set; } = true;
        public bool CopyTimestamps { get; set; } = true;
    }
}
