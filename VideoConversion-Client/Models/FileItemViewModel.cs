using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using VideoConversion_Client.Services;

namespace VideoConversion_Client.Models
{
    public class FileItemViewModel : INotifyPropertyChanged
    {
        private string _fileName = "";
        private string _filePath = "";
        private string _sourceFormat = "";
        private string _sourceResolution = "分析中...";
        private string _fileSize = "";
        private string _duration = "分析中...";
        private string _targetFormat = "MP4";
        private string _targetResolution = "1920×1080";
        private string _estimatedFileSize = "预估中...";
        private string _estimatedDuration = "预估中...";
        private FileItemStatus _status = FileItemStatus.Pending;
        private double _progress = 0;
        private string _statusText = "等待处理";
        private Bitmap? _thumbnail;
        private string? _taskId;
        private bool _isConverting = false;
        private bool _canConvert = true;
        private CancellationTokenSource? _cancellationTokenSource;

        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        public string SourceFormat
        {
            get => _sourceFormat;
            set => SetProperty(ref _sourceFormat, value);
        }

        public string SourceResolution
        {
            get => _sourceResolution;
            set => SetProperty(ref _sourceResolution, value);
        }

        public string FileSize
        {
            get => _fileSize;
            set => SetProperty(ref _fileSize, value);
        }

        public string Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        public string TargetFormat
        {
            get => _targetFormat;
            set => SetProperty(ref _targetFormat, value);
        }

        public string TargetResolution
        {
            get => _targetResolution;
            set => SetProperty(ref _targetResolution, value);
        }

        public string EstimatedFileSize
        {
            get => _estimatedFileSize;
            set => SetProperty(ref _estimatedFileSize, value);
        }

        public string EstimatedDuration
        {
            get => _estimatedDuration;
            set => SetProperty(ref _estimatedDuration, value);
        }

        public FileItemStatus Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(StatusTag));
                }
            }
        }

        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            set => SetProperty(ref _thumbnail, value);
        }

        public string? TaskId
        {
            get => _taskId;
            set => SetProperty(ref _taskId, value);
        }

        public bool IsConverting
        {
            get => _isConverting;
            set => SetProperty(ref _isConverting, value);
        }

        public bool CanConvert
        {
            get => _canConvert;
            set => SetProperty(ref _canConvert, value);
        }

        // 状态标签，用于XAML样式绑定
        public string StatusTag => Status.ToString();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// 开始转换此文件
        /// </summary>
        public async Task<bool> StartConversionAsync()
        {
            if (IsConverting || !CanConvert)
                return false;

            try
            {
                IsConverting = true;
                CanConvert = false;
                Status = FileItemStatus.Uploading;
                StatusText = "准备转换...";
                Progress = 0;

                _cancellationTokenSource = new CancellationTokenSource();

                // 获取转码设置
                var settings = ConversionSettingsService.Instance.CurrentSettings;

                var request = new StartConversionRequest
                {
                    TaskName = $"转换_{FileName}",
                    Preset = "default",
                    // 从设置中复制参数（智能格式选项将在ApiService中处理）
                    OutputFormat = settings.OutputFormat,
                    VideoCodec = settings.VideoCodec,
                    AudioCodec = settings.AudioCodec,
                    QualityMode = settings.QualityMode,
                    VideoQuality = settings.VideoQuality,
                    Resolution = settings.Resolution,
                    FrameRate = settings.FrameRate,
                    EncodingPreset = settings.EncodingPreset,
                    Profile = settings.Profile,
                    AudioQuality = settings.AudioQuality,
                    AudioQualityMode = settings.AudioQualityMode,
                    AudioChannels = settings.AudioChannels,
                    SampleRate = settings.SampleRate,
                    AudioVolume = settings.AudioVolume,
                    HardwareAcceleration = settings.HardwareAcceleration,
                    PixelFormat = settings.PixelFormat,
                    ColorSpace = settings.ColorSpace,
                    FastStart = settings.FastStart,
                    Deinterlace = settings.Deinterlace,
                    TwoPass = settings.TwoPass,
                    Denoise = settings.Denoise,
                    VideoFilters = settings.VideoFilters,
                    AudioFilters = settings.AudioFilters,
                    Priority = settings.Priority,
                    MaxRetries = settings.MaxRetries
                };

                // 创建进度报告器
                var progress = new Progress<UploadProgress>(p =>
                {
                    Progress = p.Percentage;
                    if (Status == FileItemStatus.Uploading)
                    {
                        StatusText = $"上传中... {p.Percentage:F1}%";
                    }
                    else if (Status == FileItemStatus.Converting)
                    {
                        StatusText = $"转换中... {p.Percentage:F1}%";
                    }
                });

                // 调用API开始转换
                var apiService = new ApiService();
                var result = await apiService.StartConversionAsync(FilePath, request, progress, _cancellationTokenSource.Token);

                if (result.Success && result.Data != null)
                {
                    TaskId = result.Data.TaskId;
                    Status = FileItemStatus.Converting;
                    StatusText = "转换已启动";
                    return true;
                }
                else
                {
                    Status = FileItemStatus.Failed;
                    StatusText = $"启动失败: {result.Message}";
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                Status = FileItemStatus.Cancelled;
                StatusText = "已取消";
                return false;
            }
            catch (Exception ex)
            {
                Status = FileItemStatus.Failed;
                StatusText = $"转换失败: {ex.Message}";
                return false;
            }
            finally
            {
                IsConverting = false;
                CanConvert = Status == FileItemStatus.Failed || Status == FileItemStatus.Cancelled;
            }
        }

        /// <summary>
        /// 取消转换
        /// </summary>
        public void CancelConversion()
        {
            _cancellationTokenSource?.Cancel();
            Status = FileItemStatus.Cancelled;
            StatusText = "已取消";
            IsConverting = false;
            CanConvert = true;
        }

        /// <summary>
        /// 重试转换
        /// </summary>
        public async Task<bool> RetryConversionAsync()
        {
            if (Status == FileItemStatus.Failed || Status == FileItemStatus.Cancelled)
            {
                // 重置状态
                Status = FileItemStatus.Pending;
                StatusText = "等待处理";
                Progress = 0;
                TaskId = null;
                CanConvert = true;

                // 重新开始转换
                return await StartConversionAsync();
            }
            return false;
        }

        /// <summary>
        /// 更新转换进度（由SignalR调用）
        /// </summary>
        public void UpdateProgress(double progress, string status, double? fps = null, double? eta = null)
        {
            Progress = progress;
            StatusText = status;

            if (progress >= 100)
            {
                Status = FileItemStatus.Completed;
                StatusText = "转换完成";
                IsConverting = false;
                CanConvert = false;
            }
        }

        /// <summary>
        /// 标记转换完成
        /// </summary>
        public void MarkCompleted(bool success, string? message = null)
        {
            IsConverting = false;

            if (success)
            {
                Status = FileItemStatus.Completed;
                StatusText = "转换完成";
                Progress = 100;
                CanConvert = false;
            }
            else
            {
                Status = FileItemStatus.Failed;
                StatusText = message ?? "转换失败";
                CanConvert = true;
            }
        }
    }
}
