using VideoConversion.Models;
using VideoConversion.Services;
using Microsoft.AspNetCore.SignalR;
using VideoConversion.Hubs;

namespace VideoConversion.Services
{
    /// <summary>
    /// 转换任务服务 - 处理转换任务的创建和管理
    /// </summary>
    public class ConversionTaskService
    {
        private readonly ILogger<ConversionTaskService> _logger;
        private readonly DatabaseService _databaseService;
        private readonly FileService _fileService;
        private readonly VideoConversionService _videoConversionService;
        private readonly LoggingService _loggingService;
        private readonly IHubContext<ConversionHub> _hubContext;
         
        public ConversionTaskService(
            ILogger<ConversionTaskService> logger,
            DatabaseService databaseService,
            FileService fileService,
            VideoConversionService videoConversionService,
            LoggingService loggingService,
            IHubContext<ConversionHub> hubContext)
        {
            _logger = logger;
            _databaseService = databaseService;
            _fileService = fileService;
            _videoConversionService = videoConversionService;
            _loggingService = loggingService;
            _hubContext = hubContext;
        }

        /// <summary>
        /// 从已上传文件创建转换任务
        /// </summary>
        public async Task<(bool Success, string TaskId, string TaskName, string ErrorMessage)> CreateTaskFromUploadedFile(
            string filePath, 
            string originalFileName, 
            long fileSize,
            ConversionTaskRequest request,
            string clientIp)
        {
            try
            {
                _logger.LogInformation("=== 开始从上传文件创建转换任务 ===");
                _logger.LogInformation("客户端IP: {ClientIP}", clientIp);
                _logger.LogInformation("文件路径: {FilePath}", filePath);
                _logger.LogInformation("原始文件名: {FileName}", originalFileName);
                _logger.LogInformation("任务名称: {TaskName}", request.TaskName);
                _logger.LogInformation("预设: {Preset}", request.Preset);

                // 验证文件是否存在
                if (!File.Exists(filePath))
                {
                    _logger.LogError("文件不存在: {FilePath}", filePath);
                    return (false, "", "", "文件不存在");
                }

                // 记录文件上传事件
                _loggingService.LogFileUploaded(originalFileName, fileSize, clientIp);

                // 获取转换预设作为基础
                _logger.LogInformation("获取转换预设: {PresetName}", request.Preset);

                // 获取所有可用预设
                var allPresets = ConversionPreset.GetAllPresets();

                var preset = ConversionPreset.GetPresetByName(request.Preset);
                if (preset == null)
                {
                    _logger.LogWarning("未找到预设 '{PresetName}'，使用默认预设", request.Preset);
                    preset = ConversionPreset.GetDefaultPreset();
                }

                _logger.LogInformation("使用预设: {PresetName} -> VideoCodec: {VideoCodec}, OutputFormat: {OutputFormat}",
                    preset.Name, preset.VideoCodec, preset.OutputFormat);

                // 应用详细的自定义设置
                _logger.LogInformation("应用自定义设置...");
                ApplyCustomSettings(preset, request);

                // 生成输出文件路径
                _logger.LogInformation("生成输出文件路径...");
                var outputFilePath = _fileService.GenerateOutputFilePath(
                    originalFileName,
                    preset.OutputFormat,
                    request.TaskName);
                _logger.LogInformation("输出文件路径: {OutputPath}", outputFilePath);

                // 创建转换任务
                _logger.LogInformation("开始创建转换任务对象...");

                var finalVideoCodec = preset.VideoCodec ?? "libx264";

                var task = new ConversionTask
                {
                    TaskName = !string.IsNullOrEmpty(request.TaskName) ? request.TaskName : Path.GetFileNameWithoutExtension(originalFileName) ?? "未命名任务",
                    OriginalFileName = originalFileName ?? "unknown.mkv",
                    OriginalFilePath = filePath ?? string.Empty,
                    OutputFileName = Path.GetFileName(outputFilePath) ?? "output.mp4",
                    OutputFilePath = outputFilePath ?? string.Empty,
                    OriginalFileSize = fileSize,
                    OutputFileSize = 0,
                    InputFormat = Path.GetExtension(originalFileName)?.TrimStart('.') ?? "mkv",
                    OutputFormat = preset.OutputFormat ?? "mp4",
                    VideoCodec = finalVideoCodec,
                    AudioCodec = preset.AudioCodec ?? "aac",
                    VideoQuality = preset.VideoQuality ?? "23",
                    AudioQuality = preset.AudioQuality ?? "128k",
                    Resolution = preset.Resolution ?? "1920x1080",
                    FrameRate = preset.FrameRate ?? "30",
                    Status = ConversionStatus.Pending,
                    Progress = 0,
                    ErrorMessage = string.Empty,

                    // 扩展参数
                    EncodingPreset = request.EncodingPreset ?? string.Empty,
                    Profile = request.Profile ?? string.Empty,
                    AudioChannels = request.AudioChannels ?? string.Empty,
                    SampleRate = request.SampleRate ?? string.Empty,
                    AudioVolume = request.AudioVolume?.ToString() ?? string.Empty,
                    StartTime = request.StartTime,
                    DurationLimit = request.EndTime.HasValue && request.StartTime.HasValue ? 
                        request.EndTime.Value - request.StartTime.Value : null,
                    Deinterlace = request.Deinterlace,
                    Denoise = request.Denoise ?? string.Empty,
                    ColorSpace = request.ColorSpace ?? string.Empty,
                    PixelFormat = request.PixelFormat ?? string.Empty,
                    CustomParams = request.CustomParameters ?? string.Empty,
                    FastStart = request.FastStart,
                    TwoPass = request.TwoPass,
                    CopyTimestamps = request.CopyTimestamps,
                    QualityMode = request.QualityMode ?? "CRF",
                    AudioQualityMode = request.AudioQualityMode ?? "bitrate",
                    HardwareAcceleration = request.HardwareAcceleration ?? string.Empty,
                    VideoFilters = request.VideoFilters ?? string.Empty,
                    AudioFilters = request.AudioFilters ?? string.Empty,
                    Priority = request.Priority,
                    Tags = request.Tags ?? string.Empty,
                    Notes = request.Notes ?? string.Empty,
                    CreatedAt = DateTime.Now
                };

                _logger.LogInformation("转换任务对象创建完成，准备保存到数据库...");

                // 保存任务到数据库
                var createdTask = await _databaseService.CreateTaskAsync(task);
                if (createdTask == null)
                {
                    _logger.LogError("任务保存到数据库失败");
                    return (false, "", "", "任务保存到数据库失败");
                }

                _logger.LogInformation("任务保存到数据库成功: {TaskId}", createdTask.Id);

                // 记录转换任务创建事件
                _loggingService.LogConversionStarted(createdTask.Id, createdTask.TaskName, createdTask.OriginalFileName, createdTask.OutputFormat);

                // 通知客户端任务已创建
                await _hubContext.Clients.All.SendAsync("TaskCreated", new
                {
                    TaskId = createdTask.Id,
                    TaskName = createdTask.TaskName,
                    Status = createdTask.Status.ToString(),
                    OriginalFileName = createdTask.OriginalFileName,
                    OutputFormat = createdTask.OutputFormat
                });

                // 队列服务会自动处理新任务，无需手动启动

                _logger.LogInformation("=== 转换任务创建完成 ===");
                _logger.LogInformation("任务ID: {TaskId}", createdTask.Id);
                _logger.LogInformation("任务名称: {TaskName}", createdTask.TaskName);

                return (true, createdTask.Id, createdTask.TaskName, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从上传文件创建转换任务失败: {FilePath}", filePath);
                return (false, "", "", $"创建转换任务异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用自定义设置到预设
        /// </summary>
        private void ApplyCustomSettings(ConversionPreset preset, ConversionTaskRequest request)
        {
            // 基本设置覆盖
            if (!string.IsNullOrEmpty(request.OutputFormat))
            {
                preset.OutputFormat = request.OutputFormat;
            }
            if (!string.IsNullOrEmpty(request.VideoCodec))
            {
                _logger.LogInformation("请求中的VideoCodec将覆盖预设: '{OldCodec}' -> '{NewCodec}'",
                    preset.VideoCodec, request.VideoCodec);
                preset.VideoCodec = request.VideoCodec;
            }
            if (!string.IsNullOrEmpty(request.AudioCodec))
            {
                preset.AudioCodec = request.AudioCodec;
            }
            if (!string.IsNullOrEmpty(request.VideoQuality))
                preset.VideoQuality = request.VideoQuality;
            if (!string.IsNullOrEmpty(request.AudioQuality))
                preset.AudioQuality = request.AudioQuality;
            if (!string.IsNullOrEmpty(request.Resolution))
                preset.Resolution = request.Resolution;
            if (!string.IsNullOrEmpty(request.FrameRate))
                preset.FrameRate = request.FrameRate;

            _logger.LogInformation("自定义设置应用完成");
        }
    }

    /// <summary>
    /// 转换任务请求模型
    /// </summary>
    public class ConversionTaskRequest
    {
        public string? TaskName { get; set; }
        public string Preset { get; set; } = "default";
        public string? OutputFormat { get; set; }
        public string? VideoCodec { get; set; }
        public string? AudioCodec { get; set; }
        public string? VideoQuality { get; set; }
        public string? AudioQuality { get; set; }
        public string? Resolution { get; set; }
        public string? FrameRate { get; set; }
        public string? QualityMode { get; set; }
        public string? AudioQualityMode { get; set; }
        public string? CustomParameters { get; set; }
        public string? HardwareAcceleration { get; set; }
        public string? VideoFilters { get; set; }
        public string? AudioFilters { get; set; }
        public double? StartTime { get; set; }
        public double? EndTime { get; set; }
        public int Priority { get; set; } = 0;
        public string? Tags { get; set; }
        public string? Notes { get; set; }
        public string? EncodingPreset { get; set; }
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
}
