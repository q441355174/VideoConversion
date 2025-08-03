using System;
using System.Text.Json.Serialization;

namespace VideoConversion_ClientTo.Application.DTOs
{
    /// <summary>
    /// STEP-2: 数据传输对象 - 转换任务
    /// 职责: API通信专用，与服务端数据格式对应
    /// </summary>
    public class ConversionTaskDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("taskName")]
        public string TaskName { get; set; } = string.Empty;

        [JsonPropertyName("originalFileName")]
        public string OriginalFileName { get; set; } = string.Empty;

        [JsonPropertyName("originalFilePath")]
        public string? OriginalFilePath { get; set; }

        [JsonPropertyName("outputFileName")]
        public string? OutputFileName { get; set; }

        [JsonPropertyName("outputFilePath")]
        public string? OutputFilePath { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("progress")]
        public int Progress { get; set; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("startedAt")]
        public DateTime? StartedAt { get; set; }

        [JsonPropertyName("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [JsonPropertyName("originalFileSize")]
        public long? OriginalFileSize { get; set; }

        [JsonPropertyName("outputFileSize")]
        public long? OutputFileSize { get; set; }

        [JsonPropertyName("outputFormat")]
        public string? OutputFormat { get; set; }

        [JsonPropertyName("videoCodec")]
        public string? VideoCodec { get; set; }

        [JsonPropertyName("audioCodec")]
        public string? AudioCodec { get; set; }

        [JsonPropertyName("videoQuality")]
        public string? VideoQuality { get; set; }

        [JsonPropertyName("audioQuality")]
        public string? AudioQuality { get; set; }

        [JsonPropertyName("resolution")]
        public string? Resolution { get; set; }

        [JsonPropertyName("frameRate")]
        public string? FrameRate { get; set; }

        [JsonPropertyName("conversionSpeed")]
        public double? ConversionSpeed { get; set; }

        [JsonPropertyName("estimatedTimeRemaining")]
        public int? EstimatedTimeRemaining { get; set; }

        [JsonPropertyName("duration")]
        public double? Duration { get; set; }

        [JsonPropertyName("currentTime")]
        public double? CurrentTime { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        // 兼容性属性
        public string? SourceFileName => OriginalFileName;
        public double? Speed => ConversionSpeed;
        public double? EstimatedRemainingSeconds => EstimatedTimeRemaining;
    }

    /// <summary>
    /// STEP-2: 数据传输对象 - 开始转换请求
    /// 职责: API请求参数传输
    /// </summary>
    public class StartConversionRequestDto
    {
        [JsonPropertyName("taskName")]
        public string? TaskName { get; set; }

        [JsonPropertyName("preset")]
        public string Preset { get; set; } = "Fast 1080p30";

        [JsonPropertyName("outputFormat")]
        public string? OutputFormat { get; set; }

        [JsonPropertyName("resolution")]
        public string? Resolution { get; set; }

        [JsonPropertyName("customWidth")]
        public int? CustomWidth { get; set; }

        [JsonPropertyName("customHeight")]
        public int? CustomHeight { get; set; }

        [JsonPropertyName("aspectRatio")]
        public string? AspectRatio { get; set; }

        [JsonPropertyName("videoCodec")]
        public string? VideoCodec { get; set; }

        [JsonPropertyName("audioCodec")]
        public string? AudioCodec { get; set; }

        [JsonPropertyName("videoQuality")]
        public string? VideoQuality { get; set; }

        [JsonPropertyName("audioBitrate")]
        public string? AudioBitrate { get; set; }

        [JsonPropertyName("frameRate")]
        public string? FrameRate { get; set; }

        [JsonPropertyName("encodingPreset")]
        public string? EncodingPreset { get; set; }

        [JsonPropertyName("audioSampleRate")]
        public int? AudioSampleRate { get; set; }

        [JsonPropertyName("audioChannels")]
        public int? AudioChannels { get; set; }

        [JsonPropertyName("startTime")]
        public string? StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public double? EndTime { get; set; }

        [JsonPropertyName("durationLimit")]
        public double? DurationLimit { get; set; }

        [JsonPropertyName("deinterlace")]
        public bool Deinterlace { get; set; } = false;

        [JsonPropertyName("denoise")]
        public string? Denoise { get; set; }

        [JsonPropertyName("colorSpace")]
        public string? ColorSpace { get; set; } = "bt709";

        [JsonPropertyName("pixelFormat")]
        public string? PixelFormat { get; set; } = "yuv420p";

        [JsonPropertyName("customParams")]
        public string? CustomParams { get; set; }

        [JsonPropertyName("customParameters")]
        public string? CustomParameters { get; set; }

        [JsonPropertyName("hardwareAcceleration")]
        public string HardwareAcceleration { get; set; } = "auto";

        [JsonPropertyName("videoFilters")]
        public string? VideoFilters { get; set; }

        [JsonPropertyName("audioFilters")]
        public string? AudioFilters { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 0;

        [JsonPropertyName("maxRetries")]
        public int MaxRetries { get; set; } = 3;

        [JsonPropertyName("tags")]
        public string? Tags { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("twoPass")]
        public bool TwoPass { get; set; } = false;

        [JsonPropertyName("fastStart")]
        public bool FastStart { get; set; } = true;

        [JsonPropertyName("copyTimestamps")]
        public bool CopyTimestamps { get; set; } = true;
    }

    /// <summary>
    /// STEP-2: 数据传输对象 - 开始转换响应
    /// 职责: API响应数据传输
    /// </summary>
    public class StartConversionResponseDto
    {
        [JsonPropertyName("taskId")]
        public string? TaskId { get; set; }

        [JsonPropertyName("taskName")]
        public string? TaskName { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("estimatedDuration")]
        public TimeSpan? EstimatedDuration { get; set; }
    }

    /// <summary>
    /// STEP-2: 数据传输对象 - API响应包装器
    /// 职责: 统一API响应格式
    /// </summary>
    public class ApiResponseDto<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("errorType")]
        public string? ErrorType { get; set; }

        public static ApiResponseDto<T> CreateSuccess(T data, string? message = null)
        {
            return new ApiResponseDto<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }

        public static ApiResponseDto<T> CreateError(string message, string? errorType = null)
        {
            return new ApiResponseDto<T>
            {
                Success = false,
                Message = message,
                ErrorType = errorType
            };
        }
    }

    /// <summary>
    /// 系统状态信息DTO - 与Client项目一致
    /// </summary>
    public class SystemStatusDto
    {
        [JsonPropertyName("serverVersion")]
        public string ServerVersion { get; set; } = "";

        [JsonPropertyName("ffmpegVersion")]
        public string FfmpegVersion { get; set; } = "";

        [JsonPropertyName("hardwareAcceleration")]
        public string HardwareAcceleration { get; set; } = "";

        [JsonPropertyName("uptime")]
        public string Uptime { get; set; } = "";

        [JsonPropertyName("availableDiskSpace")]
        public long AvailableDiskSpace { get; set; }

        [JsonPropertyName("totalDiskSpace")]
        public long TotalDiskSpace { get; set; }

        [JsonPropertyName("activeTasks")]
        public int ActiveTasks { get; set; }

        [JsonPropertyName("queuedTasks")]
        public int QueuedTasks { get; set; }

        [JsonPropertyName("cpuUsage")]
        public double CpuUsage { get; set; }

        [JsonPropertyName("memoryUsage")]
        public double MemoryUsage { get; set; }
    }

    /// <summary>
    /// 系统诊断信息DTO - 与Client项目一致
    /// </summary>
    public class SystemDiagnosticDto
    {
        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("level")]
        public string Level { get; set; } = "";

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("details")]
        public string? Details { get; set; }
    }

    /// <summary>
    /// 磁盘空间配置DTO - 与Client项目一致
    /// </summary>
    public class DiskSpaceConfigDto
    {
        [JsonPropertyName("minFreeSpace")]
        public long MinFreeSpace { get; set; }

        [JsonPropertyName("autoCleanup")]
        public bool AutoCleanup { get; set; }

        [JsonPropertyName("cleanupIntervalHours")]
        public int CleanupIntervalHours { get; set; }

        [JsonPropertyName("maxFileAgeHours")]
        public int MaxFileAgeHours { get; set; }

        [JsonPropertyName("cleanupPath")]
        public string CleanupPath { get; set; } = "";
    }
}
