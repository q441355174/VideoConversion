using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Domain.Models;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// 转换设置服务 - 完全按照Client项目ConversionSettingsService逻辑重构
    /// 使用现代架构但保持Client项目的所有逻辑和接口
    /// </summary>
    public class ConversionSettingsService : INotifyPropertyChanged
    {
        private static ConversionSettingsService? _instance;
        private static readonly object _lock = new object();

        private ConversionSettings _currentSettings;
        private readonly string _settingsFilePath;

        #region 单例模式 - 与Client项目完全一致

        public static ConversionSettingsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ConversionSettingsService();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region 属性 - 与Client项目ConversionSettingsService完全一致

        /// <summary>
        /// 当前转换设置
        /// </summary>
        public ConversionSettings CurrentSettings
        {
            get => _currentSettings;
            private set
            {
                if (_currentSettings != value)
                {
                    _currentSettings = value;
                    OnPropertyChanged(nameof(CurrentSettings));
                    SettingsChanged?.Invoke(this, new ConversionSettingsChangedEventArgs(_currentSettings));
                }
            }
        }

        #endregion

        #region 事件 - 与Client项目完全一致

        /// <summary>
        /// 设置变化事件
        /// </summary>
        public event EventHandler<ConversionSettingsChangedEventArgs>? SettingsChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region 构造函数 - 与Client项目逻辑完全一致

        private ConversionSettingsService()
        {
            // 设置文件路径
            _settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                           "VideoConversion", "conversion_settings.json");

            // 🔑 初始化默认设置 - 与Client项目LoadDefaultSettings()完全一致
            _currentSettings = LoadDefaultSettings();

            // 记录服务初始化 - 与Client项目一致
            Utils.Logger.Info("ConversionSettingsService", 
                $"ConversionSettingsService 已初始化，设置: {_currentSettings.VideoCodec}, {_currentSettings.Resolution}");
        }

        #endregion

        #region 静态初始化方法 - 与Client项目完全一致

        /// <summary>
        /// 初始化服务（在程序启动时调用）- 与Client项目Initialize()完全一致
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // 触发单例创建，确保服务在程序启动时就存在
                var _ = Instance;
                // ConversionSettingsService 预初始化完成（移除日志）
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsService", $"ConversionSettingsService 预初始化失败: {ex.Message}");
            }
        }

        #endregion

        #region 核心方法 - 与Client项目完全一致

        /// <summary>
        /// 加载默认设置 - 与Client项目LoadDefaultSettings()完全一致
        /// </summary>
        private ConversionSettings LoadDefaultSettings()
        {
            try
            {
                // 尝试从文件加载设置
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<ConversionSettings>(json);
                    if (settings != null)
                    {
                        Utils.Logger.Info("ConversionSettingsService", "从文件加载转换设置成功");
                        return settings;
                    }
                }

                // 🔑 如果数据库中没有设置，返回默认设置 - 与Client项目完全一致
                var defaultSettings = new ConversionSettings
                {
                    // 基本设置
                    OutputFormat = "mp4",
                    Resolution = "原始",

                    // 视频设置
                    VideoCodec = "libx264",
                    FrameRate = "原始",
                    QualityMode = "CRF",
                    VideoQuality = "23",
                    EncodingPreset = "medium",
                    Profile = "auto",

                    // 音频设置
                    AudioCodec = "aac",
                    AudioChannels = "原始",
                    AudioQuality = "192",
                    SampleRate = "48000",
                    AudioVolume = "0",

                    // 高级设置
                    HardwareAcceleration = "auto",
                    PixelFormat = "auto",
                    ColorSpace = "auto",
                    FastStart = true,
                    Deinterlace = false,
                    TwoPass = false,

                    // 滤镜设置
                    Denoise = "none",
                    VideoFilters = "",
                    AudioFilters = "",

                    // 任务设置
                    Priority = 0,
                    MaxRetries = 3
                };

                // 保存默认设置到文件
                SaveSettingsToFile(defaultSettings);
                Utils.Logger.Info("ConversionSettingsService", "使用默认转换设置并保存到文件");

                return defaultSettings;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsService", $"加载默认转换设置失败: {ex.Message}");
                return ConversionSettings.CreateDefault();
            }
        }

        /// <summary>
        /// 更新转换设置 - 与Client项目UpdateSettings()完全一致
        /// </summary>
        /// <param name="newSettings">新的转换设置</param>
        public void UpdateSettings(ConversionSettings newSettings)
        {
            try
            {
                CurrentSettings = newSettings;
                SaveSettings(newSettings);
                Utils.Logger.Info("ConversionSettingsService", "转换设置已更新");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsService", $"更新转换设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存设置 - 与Client项目SaveSettings()一致
        /// </summary>
        private void SaveSettings(ConversionSettings settings)
        {
            try
            {
                SaveSettingsToFile(settings);
                Utils.Logger.Debug("ConversionSettingsService", "转换设置已保存到文件");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsService", $"保存转换设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存设置到文件
        /// </summary>
        private void SaveSettingsToFile(ConversionSettings settings)
        {
            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 序列化并保存
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsService", $"保存设置到文件失败: {ex.Message}");
            }
        }

        #endregion

        #region INotifyPropertyChanged实现

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// 转换设置变化事件参数 - 与Client项目完全一致
    /// </summary>
    public class ConversionSettingsChangedEventArgs : EventArgs
    {
        public ConversionSettings Settings { get; }

        public ConversionSettingsChangedEventArgs(ConversionSettings settings)
        {
            Settings = settings;
        }
    }
}
