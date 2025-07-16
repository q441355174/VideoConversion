using SqlSugar;

namespace VideoConversion.Models
{
    /// <summary>
    /// 视频转换任务模型
    /// </summary>
    [SugarTable("ConversionTasks")]
    public class ConversionTask
    {
        [SugarColumn(IsPrimaryKey = true)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 任务名称
        /// </summary>
        public string TaskName { get; set; } = string.Empty;

        /// <summary>
        /// 原始文件名
        /// </summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// 原始文件路径
        /// </summary>
        public string OriginalFilePath { get; set; } = string.Empty;

        /// <summary>
        /// 输出文件名
        /// </summary>
        public string? OutputFileName { get; set; }

        /// <summary>
        /// 输出文件路径
        /// </summary>
        public string? OutputFilePath { get; set; }

        /// <summary>
        /// 原始文件大小（字节）
        /// </summary>
        public long OriginalFileSize { get; set; }

        /// <summary>
        /// 输出文件大小（字节）
        /// </summary>
        public long? OutputFileSize { get; set; }

        /// <summary>
        /// 输入格式
        /// </summary>
        public string InputFormat { get; set; } = string.Empty;

        /// <summary>
        /// 输出格式
        /// </summary>
        public string OutputFormat { get; set; } = string.Empty;

        /// <summary>
        /// 视频编解码器
        /// </summary>
        public string VideoCodec { get; set; } = string.Empty;

        /// <summary>
        /// 音频编解码器
        /// </summary>
        public string AudioCodec { get; set; } = string.Empty;

        /// <summary>
        /// 视频质量/比特率
        /// </summary>
        public string VideoQuality { get; set; } = string.Empty;

        /// <summary>
        /// 音频质量/比特率
        /// </summary>
        public string AudioQuality { get; set; } = string.Empty;

        /// <summary>
        /// 分辨率
        /// </summary>
        public string? Resolution { get; set; }

        /// <summary>
        /// 帧率
        /// </summary>
        public string? FrameRate { get; set; }

        /// <summary>
        /// 任务状态
        /// </summary>
        public ConversionStatus Status { get; set; } = ConversionStatus.Pending;

        /// <summary>
        /// 转换进度（0-100）
        /// </summary>
        public int Progress { get; set; } = 0;

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// 完成时间
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// 预计剩余时间（秒）
        /// </summary>
        public int? EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// 转换速度（倍数）
        /// </summary>
        public double? ConversionSpeed { get; set; }

        /// <summary>
        /// 视频时长（秒）
        /// </summary>
        public double? Duration { get; set; }

        /// <summary>
        /// 当前处理时间（秒）
        /// </summary>
        public double? CurrentTime { get; set; }

        // 扩展的转换参数
        /// <summary>
        /// 编码预设
        /// </summary>
        public string? EncodingPreset { get; set; }

        /// <summary>
        /// H.264配置文件
        /// </summary>
        public string? Profile { get; set; }

        /// <summary>
        /// 音频声道数
        /// </summary>
        public string? AudioChannels { get; set; }

        /// <summary>
        /// 采样率
        /// </summary>
        public string? SampleRate { get; set; }

        /// <summary>
        /// 音量调整
        /// </summary>
        public int? AudioVolume { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public string? StartTime { get; set; }

        /// <summary>
        /// 持续时间
        /// </summary>
        public string? DurationLimit { get; set; }

        /// <summary>
        /// 去隔行扫描
        /// </summary>
        public string? Deinterlace { get; set; }

        /// <summary>
        /// 降噪
        /// </summary>
        public string? Denoise { get; set; }

        /// <summary>
        /// 色彩空间
        /// </summary>
        public string? ColorSpace { get; set; }

        /// <summary>
        /// 像素格式
        /// </summary>
        public string? PixelFormat { get; set; }

        /// <summary>
        /// 自定义FFmpeg参数
        /// </summary>
        public string? CustomParams { get; set; }

        /// <summary>
        /// 是否启用两遍编码
        /// </summary>
        public bool TwoPass { get; set; } = false;

        /// <summary>
        /// 是否启用快速启动
        /// </summary>
        public bool FastStart { get; set; } = true;

        /// <summary>
        /// 是否保持时间戳
        /// </summary>
        public bool CopyTimestamps { get; set; } = true;

        /// <summary>
        /// 质量控制模式 (crf/bitrate)
        /// </summary>
        public string? QualityMode { get; set; }

        /// <summary>
        /// 音频质量模式 (bitrate/quality)
        /// </summary>
        public string? AudioQualityMode { get; set; }
    }

    /// <summary>
    /// 转换状态枚举
    /// </summary>
    public enum ConversionStatus
    {
        /// <summary>
        /// 等待中
        /// </summary>
        Pending = 0,

        /// <summary>
        /// 转换中
        /// </summary>
        Converting = 1,

        /// <summary>
        /// 已完成
        /// </summary>
        Completed = 2,

        /// <summary>
        /// 失败
        /// </summary>
        Failed = 3,

        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled = 4
    }
}
