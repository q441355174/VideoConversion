using System;
using System.ComponentModel;

namespace VideoConversion_Client.Models
{
    /// <summary>
    /// 文件项进度状态
    /// </summary>
    public enum FileItemStatus
    {
        /// <summary>
        /// 等待处理
        /// </summary>
        Pending,
        
        /// <summary>
        /// 正在上传
        /// </summary>
        Uploading,
        
        /// <summary>
        /// 上传完成，等待转换
        /// </summary>
        UploadCompleted,
        
        /// <summary>
        /// 正在转换
        /// </summary>
        Converting,
        
        /// <summary>
        /// 转换完成
        /// </summary>
        Completed,
        
        /// <summary>
        /// 失败
        /// </summary>
        Failed,
        
        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// 文件项进度信息
    /// </summary>
    public class FileItemProgress : INotifyPropertyChanged
    {
        private string _filePath = "";
        private FileItemStatus _status = FileItemStatus.Pending;
        private double _progress = 0;
        private string _statusText = "";
        private string? _errorMessage;
        private string? _taskId;
        private DateTime _startTime = DateTime.Now;
        private DateTime? _uploadCompletedTime;
        private DateTime? _conversionCompletedTime;
        private long _uploadedBytes = 0;
        private long _totalBytes = 0;
        private double _uploadSpeed = 0;
        private double _conversionSpeed = 0;
        private TimeSpan? _estimatedTimeRemaining;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged(nameof(FilePath));
                }
            }
        }

        /// <summary>
        /// 当前状态
        /// </summary>
        public FileItemStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(StatusDisplayText));
                    OnPropertyChanged(nameof(IsProcessing));
                    OnPropertyChanged(nameof(IsCompleted));
                    OnPropertyChanged(nameof(IsFailed));
                }
            }
        }

        /// <summary>
        /// 进度百分比 (0-100)
        /// </summary>
        public double Progress
        {
            get => _progress;
            set
            {
                var newValue = Math.Max(0, Math.Min(100, value));
                if (Math.Abs(_progress - newValue) > 0.01)
                {
                    _progress = newValue;
                    OnPropertyChanged(nameof(Progress));
                    OnPropertyChanged(nameof(ProgressText));
                }
            }
        }

        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged(nameof(ErrorMessage));
                }
            }
        }

        /// <summary>
        /// 任务ID
        /// </summary>
        public string? TaskId
        {
            get => _taskId;
            set
            {
                if (_taskId != value)
                {
                    _taskId = value;
                    OnPropertyChanged(nameof(TaskId));
                }
            }
        }

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime StartTime
        {
            get => _startTime;
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    OnPropertyChanged(nameof(StartTime));
                }
            }
        }

        /// <summary>
        /// 上传完成时间
        /// </summary>
        public DateTime? UploadCompletedTime
        {
            get => _uploadCompletedTime;
            set
            {
                if (_uploadCompletedTime != value)
                {
                    _uploadCompletedTime = value;
                    OnPropertyChanged(nameof(UploadCompletedTime));
                    OnPropertyChanged(nameof(UploadDuration));
                }
            }
        }

        /// <summary>
        /// 转换完成时间
        /// </summary>
        public DateTime? ConversionCompletedTime
        {
            get => _conversionCompletedTime;
            set
            {
                if (_conversionCompletedTime != value)
                {
                    _conversionCompletedTime = value;
                    OnPropertyChanged(nameof(ConversionCompletedTime));
                    OnPropertyChanged(nameof(ConversionDuration));
                    OnPropertyChanged(nameof(TotalDuration));
                }
            }
        }

        /// <summary>
        /// 已上传字节数
        /// </summary>
        public long UploadedBytes
        {
            get => _uploadedBytes;
            set
            {
                if (_uploadedBytes != value)
                {
                    _uploadedBytes = value;
                    OnPropertyChanged(nameof(UploadedBytes));
                    OnPropertyChanged(nameof(UploadProgressText));
                }
            }
        }

        /// <summary>
        /// 总字节数
        /// </summary>
        public long TotalBytes
        {
            get => _totalBytes;
            set
            {
                if (_totalBytes != value)
                {
                    _totalBytes = value;
                    OnPropertyChanged(nameof(TotalBytes));
                    OnPropertyChanged(nameof(UploadProgressText));
                }
            }
        }

        /// <summary>
        /// 上传速度 (字节/秒)
        /// </summary>
        public double UploadSpeed
        {
            get => _uploadSpeed;
            set
            {
                if (Math.Abs(_uploadSpeed - value) > 0.01)
                {
                    _uploadSpeed = value;
                    OnPropertyChanged(nameof(UploadSpeed));
                    OnPropertyChanged(nameof(UploadSpeedText));
                }
            }
        }

        /// <summary>
        /// 转换速度
        /// </summary>
        public double ConversionSpeed
        {
            get => _conversionSpeed;
            set
            {
                if (Math.Abs(_conversionSpeed - value) > 0.01)
                {
                    _conversionSpeed = value;
                    OnPropertyChanged(nameof(ConversionSpeed));
                    OnPropertyChanged(nameof(ConversionSpeedText));
                }
            }
        }

        /// <summary>
        /// 预计剩余时间
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining
        {
            get => _estimatedTimeRemaining;
            set
            {
                if (_estimatedTimeRemaining != value)
                {
                    _estimatedTimeRemaining = value;
                    OnPropertyChanged(nameof(EstimatedTimeRemaining));
                    OnPropertyChanged(nameof(EstimatedTimeRemainingText));
                }
            }
        }

        // 计算属性

        /// <summary>
        /// 状态显示文本
        /// </summary>
        public string StatusDisplayText => Status switch
        {
            FileItemStatus.Pending => "等待处理",
            FileItemStatus.Uploading => "正在上传",
            FileItemStatus.UploadCompleted => "上传完成",
            FileItemStatus.Converting => "正在转换",
            FileItemStatus.Completed => "转换完成",
            FileItemStatus.Failed => "处理失败",
            FileItemStatus.Cancelled => "已取消",
            _ => "未知状态"
        };

        /// <summary>
        /// 进度文本
        /// </summary>
        public string ProgressText => $"{Progress:F1}%";

        /// <summary>
        /// 上传进度文本
        /// </summary>
        public string UploadProgressText
        {
            get
            {
                if (TotalBytes > 0)
                {
                    var uploadedSize = Utils.FileSizeFormatter.FormatBytesAuto(UploadedBytes);
                    var totalSize = Utils.FileSizeFormatter.FormatBytesAuto(TotalBytes);
                    return $"{uploadedSize} / {totalSize}";
                }
                return "";
            }
        }

        /// <summary>
        /// 上传速度文本
        /// </summary>
        public string UploadSpeedText
        {
            get
            {
                if (UploadSpeed > 0)
                {
                    return Utils.FileSizeFormatter.FormatBytesAuto((long)UploadSpeed) + "/s";
                }
                return "";
            }
        }

        /// <summary>
        /// 转换速度文本
        /// </summary>
        public string ConversionSpeedText
        {
            get
            {
                if (ConversionSpeed > 0)
                {
                    return $"{ConversionSpeed:F2}x";
                }
                return "";
            }
        }

        /// <summary>
        /// 预计剩余时间文本
        /// </summary>
        public string EstimatedTimeRemainingText
        {
            get
            {
                if (EstimatedTimeRemaining.HasValue)
                {
                    var time = EstimatedTimeRemaining.Value;
                    if (time.TotalHours >= 1)
                        return $"{time:hh\\:mm\\:ss}";
                    else
                        return $"{time:mm\\:ss}";
                }
                return "";
            }
        }

        /// <summary>
        /// 上传耗时
        /// </summary>
        public TimeSpan? UploadDuration
        {
            get
            {
                if (UploadCompletedTime.HasValue)
                {
                    return UploadCompletedTime.Value - StartTime;
                }
                return null;
            }
        }

        /// <summary>
        /// 转换耗时
        /// </summary>
        public TimeSpan? ConversionDuration
        {
            get
            {
                if (ConversionCompletedTime.HasValue && UploadCompletedTime.HasValue)
                {
                    return ConversionCompletedTime.Value - UploadCompletedTime.Value;
                }
                return null;
            }
        }

        /// <summary>
        /// 总耗时
        /// </summary>
        public TimeSpan? TotalDuration
        {
            get
            {
                if (ConversionCompletedTime.HasValue)
                {
                    return ConversionCompletedTime.Value - StartTime;
                }
                return null;
            }
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing => Status == FileItemStatus.Uploading || Status == FileItemStatus.Converting;

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted => Status == FileItemStatus.Completed;

        /// <summary>
        /// 是否失败
        /// </summary>
        public bool IsFailed => Status == FileItemStatus.Failed;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
