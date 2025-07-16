using SqlSugar;

namespace VideoConversion.Models
{
    /// <summary>
    /// 视频转换任务模型
    /// </summary>
    [SugarTable("ConversionTasks")]
    public class ConversionTask
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsNullable = false)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 任务名称
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string TaskName { get; set; } = string.Empty;

        /// <summary>
        /// 原始文件名
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// 原始文件路径
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string OriginalFilePath { get; set; } = string.Empty;

        /// <summary>
        /// 输出文件名
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string OutputFileName { get; set; } = string.Empty;

        /// <summary>
        /// 输出文件路径
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string OutputFilePath { get; set; } = string.Empty;

        /// <summary>
        /// 原始文件大小（字节）
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long OriginalFileSize { get; set; } = 0;

        /// <summary>
        /// 输出文件大小（字节）
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long OutputFileSize { get; set; } = 0;

        /// <summary>
        /// 输入格式
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string InputFormat { get; set; } = string.Empty;

        /// <summary>
        /// 输出格式
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string OutputFormat { get; set; } = string.Empty;

        /// <summary>
        /// 视频编解码器
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string VideoCodec { get; set; } = string.Empty;

        /// <summary>
        /// 音频编解码器
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string AudioCodec { get; set; } = string.Empty;

        /// <summary>
        /// 视频质量/比特率
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string VideoQuality { get; set; } = string.Empty;

        /// <summary>
        /// 音频质量/比特率
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string AudioQuality { get; set; } = string.Empty;

        /// <summary>
        /// 分辨率
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string Resolution { get; set; } = string.Empty;

        /// <summary>
        /// 帧率
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string FrameRate { get; set; } = string.Empty;

        /// <summary>
        /// 任务状态
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public ConversionStatus Status { get; set; } = ConversionStatus.Pending;

        /// <summary>
        /// 转换进度（0-100）
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int Progress { get; set; } = 0;

        /// <summary>
        /// 错误信息
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 开始时间
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// 完成时间
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// 预计剩余时间（秒）
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public int? EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// 转换速度（倍数）
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public double? ConversionSpeed { get; set; }

        /// <summary>
        /// 视频时长（秒）
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public double? Duration { get; set; }

        /// <summary>
        /// 当前处理时间（秒）
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public double? CurrentTime { get; set; }

        // 扩展的转换参数
        /// <summary>
        /// 编码预设
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string EncodingPreset { get; set; } = string.Empty;

        /// <summary>
        /// 质量模式
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string QualityMode { get; set; } = string.Empty;

        /// <summary>
        /// 音频质量模式
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string AudioQualityMode { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用两遍编码
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool TwoPass { get; set; } = false;

        /// <summary>
        /// 是否启用快速启动
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool FastStart { get; set; } = false;

        /// <summary>
        /// 是否复制时间戳
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool CopyTimestamps { get; set; } = false;

        /// <summary>
        /// 自定义FFmpeg参数
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string CustomParameters { get; set; } = string.Empty;

        /// <summary>
        /// 硬件加速类型
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string HardwareAcceleration { get; set; } = string.Empty;

        /// <summary>
        /// 视频滤镜
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string VideoFilters { get; set; } = string.Empty;

        /// <summary>
        /// 音频滤镜
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string AudioFilters { get; set; } = string.Empty;

        /// <summary>
        /// 开始时间（裁剪）
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public double? StartTime { get; set; }

        /// <summary>
        /// 结束时间（裁剪）
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public double? EndTime { get; set; }

        /// <summary>
        /// 优先级
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int Priority { get; set; } = 0;

        /// <summary>
        /// 重试次数
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// 最大重试次数
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// 标签
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string Tags { get; set; } = string.Empty;

        /// <summary>
        /// 备注
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string Notes { get; set; } = string.Empty;

        // 额外的转换参数
        /// <summary>
        /// 编码配置文件
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string Profile { get; set; } = string.Empty;

        /// <summary>
        /// 音频声道数
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string AudioChannels { get; set; } = string.Empty;

        /// <summary>
        /// 采样率
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string SampleRate { get; set; } = string.Empty;

        /// <summary>
        /// 音频音量
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string AudioVolume { get; set; } = string.Empty;

        /// <summary>
        /// 时长限制
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public double? DurationLimit { get; set; }

        /// <summary>
        /// 去隔行扫描
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool Deinterlace { get; set; } = false;

        /// <summary>
        /// 降噪
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string Denoise { get; set; } = string.Empty;

        /// <summary>
        /// 色彩空间
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string ColorSpace { get; set; } = string.Empty;

        /// <summary>
        /// 像素格式
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string PixelFormat { get; set; } = string.Empty;

        /// <summary>
        /// 自定义参数
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string CustomParams { get; set; } = string.Empty;
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
