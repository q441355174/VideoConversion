using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace VideoConversion_Client.Models
{
    /// <summary>
    /// 转换任务模型（客户端版本）
    /// </summary>
    public class ConversionTask : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _taskName = string.Empty;
        private string _originalFileName = string.Empty;
        private string _outputFileName = string.Empty;
        private string? _outputPath;
        private ConversionStatus _status = ConversionStatus.Pending;
        private int _progress = 0;
        private string? _errorMessage;
        private DateTime _createdAt = DateTime.Now;
        private DateTime? _startedAt;
        private DateTime? _completedAt;
        private int? _estimatedTimeRemaining;
        private double? _conversionSpeed;
        private double? _duration;
        private double? _currentTime;
        private string? _inputFormat;
        private string? _outputFormat;
        private string? _resolution;
        private long? _originalFileSize;
        private long? _outputFileSize;

        // 新增字段以匹配服务端模型
        private string? _originalFilePath;
        private string? _outputFilePath;
        private string? _videoCodec;
        private string? _audioCodec;
        private string? _videoQuality;
        private string? _audioQuality;
        private string? _frameRate;
        private string? _notes;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string TaskName
        {
            get => _taskName;
            set => SetProperty(ref _taskName, value);
        }

        public string OriginalFileName
        {
            get => _originalFileName;
            set => SetProperty(ref _originalFileName, value);
        }

        public string OutputFileName
        {
            get => _outputFileName;
            set => SetProperty(ref _outputFileName, value);
        }

        public string? OutputPath
        {
            get => _outputPath;
            set => SetProperty(ref _outputPath, value);
        }

        public ConversionStatus Status
        {
            get => _status;
            set
            {
                SetProperty(ref _status, value);
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsCompleted));
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanDownload));
            }
        }

        public int Progress
        {
            get => _progress;
            set
            {
                SetProperty(ref _progress, value);
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        public DateTime? StartedAt
        {
            get => _startedAt;
            set => SetProperty(ref _startedAt, value);
        }

        public DateTime? CompletedAt
        {
            get => _completedAt;
            set => SetProperty(ref _completedAt, value);
        }

        public int? EstimatedTimeRemaining
        {
            get => _estimatedTimeRemaining;
            set
            {
                SetProperty(ref _estimatedTimeRemaining, value);
                OnPropertyChanged(nameof(RemainingTimeText));
            }
        }

        public double? ConversionSpeed
        {
            get => _conversionSpeed;
            set
            {
                SetProperty(ref _conversionSpeed, value);
                OnPropertyChanged(nameof(SpeedText));
            }
        }

        public double? Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        public double? CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        public string? InputFormat
        {
            get => _inputFormat;
            set => SetProperty(ref _inputFormat, value);
        }

        public string? OutputFormat
        {
            get => _outputFormat;
            set => SetProperty(ref _outputFormat, value);
        }

        public string? Resolution
        {
            get => _resolution;
            set => SetProperty(ref _resolution, value);
        }

        public long? OriginalFileSize
        {
            get => _originalFileSize;
            set => SetProperty(ref _originalFileSize, value);
        }

        public long? OutputFileSize
        {
            get => _outputFileSize;
            set => SetProperty(ref _outputFileSize, value);
        }

        // 新增属性以匹配服务端模型
        public string? OriginalFilePath
        {
            get => _originalFilePath;
            set => SetProperty(ref _originalFilePath, value);
        }

        public string? OutputFilePath
        {
            get => _outputFilePath;
            set => SetProperty(ref _outputFilePath, value);
        }

        public string? VideoCodec
        {
            get => _videoCodec;
            set => SetProperty(ref _videoCodec, value);
        }

        public string? AudioCodec
        {
            get => _audioCodec;
            set => SetProperty(ref _audioCodec, value);
        }

        public string? VideoQuality
        {
            get => _videoQuality;
            set => SetProperty(ref _videoQuality, value);
        }

        public string? AudioQuality
        {
            get => _audioQuality;
            set => SetProperty(ref _audioQuality, value);
        }

        public string? FrameRate
        {
            get => _frameRate;
            set => SetProperty(ref _frameRate, value);
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        // 计算属性
        public string StatusText => Status switch
        {
            ConversionStatus.Pending => "等待中",
            ConversionStatus.Converting => "转换中",
            ConversionStatus.Completed => "已完成",
            ConversionStatus.Failed => "失败",
            ConversionStatus.Cancelled => "已取消",
            _ => "未知"
        };

        public string ProgressText => $"{Progress}%";

        public string RemainingTimeText => EstimatedTimeRemaining.HasValue
            ? TimeSpan.FromSeconds(EstimatedTimeRemaining.Value).ToString(@"hh\:mm\:ss")
            : "--:--:--";

        public string SpeedText => ConversionSpeed.HasValue
            ? $"{ConversionSpeed.Value:F1}x"
            : "--";

        public bool IsCompleted => Status == ConversionStatus.Completed;
        public bool IsRunning => Status == ConversionStatus.Converting;
        public bool CanCancel => Status == ConversionStatus.Pending || Status == ConversionStatus.Converting;
        public bool CanDownload => Status == ConversionStatus.Completed;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// 转换状态枚举 - 与服务端保持兼容
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
        Cancelled = 4,

        /// <summary>
        /// 已暂停
        /// </summary>
        Paused = 5,

        /// <summary>
        /// 上传中 - 客户端专用状态
        /// </summary>
        Uploading = 10
    }

    /// <summary>
    /// 转换预设
    /// </summary>
    public class ConversionPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string OutputFormat { get; set; } = string.Empty;
        public string VideoCodec { get; set; } = string.Empty;
        public string AudioCodec { get; set; } = string.Empty;
        public string VideoQuality { get; set; } = string.Empty;
        public string AudioQuality { get; set; } = string.Empty;
        public string? Resolution { get; set; }
        public string? FrameRate { get; set; }
        public bool IsDefault { get; set; } = false;

        public static List<ConversionPreset> GetAllPresets()
        {
            return new List<ConversionPreset>
            {
                new ConversionPreset
                {
                    Name = "Fast 1080p30",
                    Description = "快速转换为1080p 30fps，适合快速预览",
                    OutputFormat = "mp4",
                    VideoCodec = "libx264",
                    AudioCodec = "aac",
                    VideoQuality = "23",
                    AudioQuality = "128k",
                    Resolution = "1920x1080",
                    FrameRate = "30",
                    IsDefault = true
                },
                new ConversionPreset
                {
                    Name = "High Quality 1080p",
                    Description = "高质量1080p，文件较大但质量最佳",
                    OutputFormat = "mp4",
                    VideoCodec = "libx264",
                    AudioCodec = "aac",
                    VideoQuality = "18",
                    AudioQuality = "192k",
                    Resolution = "1920x1080"
                },
                new ConversionPreset
                {
                    Name = "Web Optimized",
                    Description = "网络优化版本，适合在线播放",
                    OutputFormat = "mp4",
                    VideoCodec = "libx264",
                    AudioCodec = "aac",
                    VideoQuality = "25",
                    AudioQuality = "128k",
                    Resolution = "1280x720"
                },
                new ConversionPreset
                {
                    Name = "Mobile Friendly",
                    Description = "适合移动设备播放",
                    OutputFormat = "mp4",
                    VideoCodec = "libx264",
                    AudioCodec = "aac",
                    VideoQuality = "23",
                    AudioQuality = "128k",
                    Resolution = "1920x1080"
                },
                new ConversionPreset
                {
                    Name = "4K Ultra HD",
                    Description = "4K超高清质量",
                    OutputFormat = "mp4",
                    VideoCodec = "libx264",
                    AudioCodec = "aac",
                    VideoQuality = "20",
                    AudioQuality = "256k",
                    Resolution = "3840x2160"
                }
            };
        }

        public static ConversionPreset? GetPresetByName(string name)
        {
            return GetAllPresets().FirstOrDefault(p => p.Name == name);
        }

        public static ConversionPreset GetDefaultPreset()
        {
            return GetAllPresets().First(p => p.IsDefault);
        }
    }

    /// <summary>
    /// API响应模型
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public string? ErrorType { get; set; }

        /// <summary>
        /// 创建成功响应
        /// </summary>
        public static ApiResponse<T> CreateSuccess(T data, string message = "操作成功")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        /// <summary>
        /// 创建错误响应
        /// </summary>
        public static ApiResponse<T> CreateError(string message, T? data = default)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = data
            };
        }
    }

    /// <summary>
    /// 开始转换请求模型（与服务器端ConversionTask对应）
    /// </summary>
    public class StartConversionRequest
    {
        // 基本信息
        public string? TaskName { get; set; }
        public string Preset { get; set; } = "Fast 1080p30";

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
        public double? DurationLimit { get; set; }
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
        public int MaxRetries { get; set; } = 3;
        public string? Tags { get; set; }
        public string? Notes { get; set; }

        // 编码选项
        public bool TwoPass { get; set; } = false;
        public bool FastStart { get; set; } = true;
        public bool CopyTimestamps { get; set; } = true;
    }

    /// <summary>
    /// 转换设置模型（与服务器端ConversionTask完全对应）
    /// </summary>
    public class ConversionSettings
    {
        // 基本设置
        public string? OutputFormat { get; set; } = "MP4";
        public string Resolution { get; set; } = "1920x1080";

        // 视频设置
        public string VideoCodec { get; set; } = "H.264";
        public string FrameRate { get; set; } = "30";
        public string? QualityMode { get; set; } = "crf";
        public string? VideoQuality { get; set; } = "23";
        public string? EncodingPreset { get; set; } = "medium";
        public string? Profile { get; set; }

        // 音频设置
        public string AudioCodec { get; set; } = "AAC";
        public string? AudioChannels { get; set; } = "2";
        public string? AudioQualityMode { get; set; } = "bitrate";
        public string AudioQuality { get; set; } = "128k";
        public string? SampleRate { get; set; } = "48000";
        public string? AudioVolume { get; set; } = "0";

        // 高级选项
        public string? StartTime { get; set; }
        public double? EndTime { get; set; }
        public double? DurationLimit { get; set; }
        public bool Deinterlace { get; set; } = false;
        public string? Denoise { get; set; }
        public string? ColorSpace { get; set; } = "bt709";
        public string? PixelFormat { get; set; } = "yuv420p";
        public string? CustomParams { get; set; }
        public string? CustomParameters { get; set; }
        public string HardwareAcceleration { get; set; } = "auto";
        public string? VideoFilters { get; set; }
        public string? AudioFilters { get; set; }

        // 任务设置
        public int Priority { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        public string? Tags { get; set; }
        public string? Notes { get; set; }

        // 编码选项
        public bool TwoPass { get; set; } = false;
        public bool FastStart { get; set; } = true;
        public bool CopyTimestamps { get; set; } = true;
    }

    /// <summary>
    /// 转换开始事件参数（与StartConversionRequest对应）
    /// </summary>
    public class ConversionStartEventArgs : EventArgs
    {
        // 基本信息
        public string? TaskName { get; set; }
        public string Preset { get; set; } = "Fast 1080p30";

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
        public double? DurationLimit { get; set; }
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
        public int MaxRetries { get; set; } = 3;
        public string? Tags { get; set; }
        public string? Notes { get; set; }

        // 编码选项
        public bool TwoPass { get; set; } = false;
        public bool FastStart { get; set; } = true;
        public bool CopyTimestamps { get; set; } = true;
    }
}
