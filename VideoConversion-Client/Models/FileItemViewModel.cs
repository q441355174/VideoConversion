using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;

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
    }


}
