using System;
using System.ComponentModel;

namespace VideoConversion_ClientTo.Domain.Models
{
    /// <summary>
    /// 转换设置模型 - 与Client项目ConversionSettings完全兼容
    /// 使用现代架构但保持Client项目的所有属性和逻辑
    /// </summary>
    public class ConversionSettings : INotifyPropertyChanged, IEquatable<ConversionSettings>
    {
        #region 私有字段

        private string _outputFormat = "mp4";
        private string _resolution = "1920x1080";
        private string _videoCodec = "libx264";
        private string _frameRate = "保持原始";
        private string _qualityMode = "恒定质量 (CRF)";
        private string _videoQuality = "23";
        private string _encodingPreset = "中等 (推荐)";
        private string _profile = "High";
        private string _audioCodec = "aac";
        private string _audioQuality = "192k";
        private string _audioChannels = "保持原始";
        private string _sampleRate = "48000";
        private string _audioVolume = "0";
        private string _hardwareAcceleration = "自动检测";
        private string _pixelFormat = "YUV420P (标准)";
        private string _colorSpace = "BT.709 (HD)";
        private bool _fastStart = true;
        private bool _deinterlace = false;
        private bool _twoPass = false;
        private string _denoise = "无";
        private string _videoFilters = "";
        private string _audioFilters = "";
        private int _priority = 0;
        private int _maxRetries = 3;
        private string _preset = "CPU Standard 1080p";

        #endregion

        #region 属性 - 与Client项目ConversionSettings完全一致

        /// <summary>
        /// 输出格式
        /// </summary>
        public string OutputFormat
        {
            get => _outputFormat;
            set => SetProperty(ref _outputFormat, value);
        }

        /// <summary>
        /// 分辨率
        /// </summary>
        public string Resolution
        {
            get => _resolution;
            set => SetProperty(ref _resolution, value);
        }

        /// <summary>
        /// 视频编码器
        /// </summary>
        public string VideoCodec
        {
            get => _videoCodec;
            set => SetProperty(ref _videoCodec, value);
        }

        /// <summary>
        /// 帧率
        /// </summary>
        public string FrameRate
        {
            get => _frameRate;
            set => SetProperty(ref _frameRate, value);
        }

        /// <summary>
        /// 质量模式
        /// </summary>
        public string QualityMode
        {
            get => _qualityMode;
            set => SetProperty(ref _qualityMode, value);
        }

        /// <summary>
        /// 视频质量
        /// </summary>
        public string VideoQuality
        {
            get => _videoQuality;
            set => SetProperty(ref _videoQuality, value);
        }

        /// <summary>
        /// 编码预设
        /// </summary>
        public string EncodingPreset
        {
            get => _encodingPreset;
            set => SetProperty(ref _encodingPreset, value);
        }

        /// <summary>
        /// 配置文件
        /// </summary>
        public string Profile
        {
            get => _profile;
            set => SetProperty(ref _profile, value);
        }

        /// <summary>
        /// 音频编码器
        /// </summary>
        public string AudioCodec
        {
            get => _audioCodec;
            set => SetProperty(ref _audioCodec, value);
        }

        /// <summary>
        /// 音频质量
        /// </summary>
        public string AudioQuality
        {
            get => _audioQuality;
            set => SetProperty(ref _audioQuality, value);
        }

        /// <summary>
        /// 音频声道
        /// </summary>
        public string AudioChannels
        {
            get => _audioChannels;
            set => SetProperty(ref _audioChannels, value);
        }

        /// <summary>
        /// 采样率
        /// </summary>
        public string SampleRate
        {
            get => _sampleRate;
            set => SetProperty(ref _sampleRate, value);
        }

        /// <summary>
        /// 音量调整
        /// </summary>
        public string AudioVolume
        {
            get => _audioVolume;
            set => SetProperty(ref _audioVolume, value);
        }

        /// <summary>
        /// 硬件加速
        /// </summary>
        public string HardwareAcceleration
        {
            get => _hardwareAcceleration;
            set => SetProperty(ref _hardwareAcceleration, value);
        }

        /// <summary>
        /// 像素格式
        /// </summary>
        public string PixelFormat
        {
            get => _pixelFormat;
            set => SetProperty(ref _pixelFormat, value);
        }

        /// <summary>
        /// 色彩空间
        /// </summary>
        public string ColorSpace
        {
            get => _colorSpace;
            set => SetProperty(ref _colorSpace, value);
        }

        /// <summary>
        /// 快速启动
        /// </summary>
        public bool FastStart
        {
            get => _fastStart;
            set => SetProperty(ref _fastStart, value);
        }

        /// <summary>
        /// 去隔行
        /// </summary>
        public bool Deinterlace
        {
            get => _deinterlace;
            set => SetProperty(ref _deinterlace, value);
        }

        /// <summary>
        /// 两遍编码
        /// </summary>
        public bool TwoPass
        {
            get => _twoPass;
            set => SetProperty(ref _twoPass, value);
        }

        /// <summary>
        /// 降噪滤镜
        /// </summary>
        public string Denoise
        {
            get => _denoise;
            set => SetProperty(ref _denoise, value);
        }

        /// <summary>
        /// 视频滤镜
        /// </summary>
        public string VideoFilters
        {
            get => _videoFilters;
            set => SetProperty(ref _videoFilters, value);
        }

        /// <summary>
        /// 音频滤镜
        /// </summary>
        public string AudioFilters
        {
            get => _audioFilters;
            set => SetProperty(ref _audioFilters, value);
        }

        /// <summary>
        /// 优先级
        /// </summary>
        public int Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetries
        {
            get => _maxRetries;
            set => SetProperty(ref _maxRetries, value);
        }

        /// <summary>
        /// 预设名称
        /// </summary>
        public string Preset
        {
            get => _preset;
            set => SetProperty(ref _preset, value);
        }

        #endregion

        #region INotifyPropertyChanged实现

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region 相等性比较

        public bool Equals(ConversionSettings? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            
            return OutputFormat == other.OutputFormat &&
                   Resolution == other.Resolution &&
                   VideoCodec == other.VideoCodec &&
                   AudioCodec == other.AudioCodec &&
                   VideoQuality == other.VideoQuality &&
                   AudioQuality == other.AudioQuality &&
                   EncodingPreset == other.EncodingPreset &&
                   FrameRate == other.FrameRate &&
                   QualityMode == other.QualityMode;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ConversionSettings);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OutputFormat, Resolution, VideoCodec, AudioCodec, VideoQuality, AudioQuality, EncodingPreset, FrameRate);
        }

        public override string ToString()
        {
            return $"{OutputFormat} {Resolution} ({VideoCodec}/{AudioCodec}) Q:{VideoQuality}";
        }

        #endregion

        #region 工厂方法 - 与Client项目一致

        /// <summary>
        /// 创建默认设置 - 与Client项目LoadDefaultSettings()一致
        /// </summary>
        public static ConversionSettings CreateDefault()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "保持原始",
                VideoCodec = "H.264 (CPU)",
                FrameRate = "保持原始",
                QualityMode = "恒定质量 (CRF)",
                VideoQuality = "23",
                EncodingPreset = "中等 (推荐)",
                Profile = "High",
                AudioCodec = "AAC (推荐)",
                AudioQuality = "192 kbps (高质量)",
                AudioChannels = "保持原始",
                SampleRate = "48 kHz (DVD质量)",
                AudioVolume = "0",
                HardwareAcceleration = "自动检测",
                PixelFormat = "YUV420P (标准)",
                ColorSpace = "BT.709 (HD)",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "无",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        #endregion

        #region 转换方法 - 与ConversionParameters兼容

        /// <summary>
        /// 转换为ConversionParameters值对象
        /// </summary>
        /// <returns>ConversionParameters实例</returns>
        public VideoConversion_ClientTo.Domain.ValueObjects.ConversionParameters ToConversionParameters()
        {
            try
            {
                return VideoConversion_ClientTo.Domain.ValueObjects.ConversionParameters.Create(
                    outputFormat: OutputFormat ?? "mp4",
                    resolution: Resolution ?? "保持原始",
                    videoCodec: VideoCodec ?? "H.264 (CPU)",
                    audioCodec: AudioCodec ?? "AAC (推荐)",
                    videoQuality: VideoQuality ?? "23",
                    audioQuality: AudioQuality ?? "192 kbps (高质量)",
                    preset: Preset ?? "CPU Standard 1080p"
                );
            }
            catch (Exception ex)
            {
                // 如果转换失败，返回默认参数
                Utils.Logger.Error("ConversionSettings", $"转换为ConversionParameters失败: {ex.Message}");
                return VideoConversion_ClientTo.Domain.ValueObjects.ConversionParameters.CreateDefault();
            }
        }

        /// <summary>
        /// 从ConversionParameters创建ConversionSettings
        /// </summary>
        /// <param name="parameters">ConversionParameters实例</param>
        /// <returns>ConversionSettings实例</returns>
        public static ConversionSettings FromConversionParameters(VideoConversion_ClientTo.Domain.ValueObjects.ConversionParameters parameters)
        {
            try
            {
                return new ConversionSettings
                {
                    OutputFormat = parameters.OutputFormat,
                    Resolution = parameters.Resolution,
                    VideoCodec = parameters.VideoCodec,
                    AudioCodec = parameters.AudioCodec,
                    VideoQuality = parameters.VideoQuality,
                    AudioQuality = parameters.AudioQuality,
                    Preset = parameters.Preset,

                    // 设置其他默认值
                    FrameRate = "保持原始",
                    QualityMode = "恒定质量 (CRF)",
                    EncodingPreset = "中等 (推荐)",
                    Profile = "auto",
                    AudioChannels = "保持原始",
                    SampleRate = "48 kHz (DVD质量)",
                    AudioVolume = "0",
                    HardwareAcceleration = "自动检测",
                    PixelFormat = "YUV420P (标准)",
                    ColorSpace = "自动",
                    FastStart = true,
                    Deinterlace = false,
                    TwoPass = false,
                    Denoise = "无",
                    VideoFilters = "",
                    AudioFilters = "",
                    Priority = 0,
                    MaxRetries = 3
                };
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettings", $"从ConversionParameters创建失败: {ex.Message}");
                return CreateDefault();
            }
        }

        #endregion
    }
}
