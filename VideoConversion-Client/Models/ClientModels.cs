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
    /// 转换状态枚举
    /// </summary>
    public enum ConversionStatus
    {
        Pending = 0,
        Converting = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4
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
    }

    /// <summary>
    /// 开始转换请求模型
    /// </summary>
    public class StartConversionRequest
    {
        public string? TaskName { get; set; }
        public string Preset { get; set; } = "Fast 1080p30";
        public string? OutputFormat { get; set; }
        public string? Resolution { get; set; }
        public string? VideoCodec { get; set; }
        public string? AudioCodec { get; set; }
        public string? VideoQuality { get; set; }
        public string? AudioQuality { get; set; }
        public string? FrameRate { get; set; }
    }
}
