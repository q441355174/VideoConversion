using System;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using VideoConversion_Client.Services;
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
        private readonly SqlSugarDatabaseService _databaseService;

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
            // 🔧 初始化数据库服务
            _databaseService = SqlSugarDatabaseService.Instance;

            // 🔑 从数据库加载设置，如果不存在则使用默认设置
            _currentSettings = LoadSettingsFromDatabase();

            // 记录服务初始化
            Utils.Logger.Info("ConversionSettingsService",
                $"ConversionSettingsService 已初始化，设置: {_currentSettings.VideoCodec}, {_currentSettings.Resolution}");
        }

        /// <summary>
        /// 从数据库加载转换设置（同步版本，用于初始化）
        /// </summary>
        private ConversionSettings LoadSettingsFromDatabase()
        {
            try
            {
                Utils.Logger.Info("ConversionSettingsService", "🔍 从数据库加载转换设置");

                // 🔧 使用同步方式获取数据库设置（初始化时使用）
                // 注意：这里使用Task.Run().Result是为了在构造函数中同步获取数据
                var settingsJson = Task.Run(async () =>
                {
                    try
                    {
                        return await _databaseService.GetSettingAsync("ConversionSettings");
                    }
                    catch
                    {
                        return null;
                    }
                }).Result;

                if (!string.IsNullOrEmpty(settingsJson))
                {
                    var settings = JsonSerializer.Deserialize<ConversionSettings>(settingsJson);
                    if (settings != null)
                    {
                        Utils.Logger.Info("ConversionSettingsService", "✅ 从数据库成功加载转换设置");
                        return settings;
                    }
                }

                // 如果数据库中没有设置，创建并保存默认设置
                Utils.Logger.Info("ConversionSettingsService", "📝 数据库中无转换设置，创建默认设置");
                var defaultSettings = CreateDefaultSettings();

                // 异步保存到数据库（不等待结果）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SaveSettingsToDatabaseAsync(defaultSettings);
                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Error("ConversionSettingsService", $"❌ 保存默认设置到数据库失败: {ex.Message}");
                    }
                });

                return defaultSettings;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsService", $"❌ 从数据库加载转换设置失败: {ex.Message}");
                return CreateDefaultSettings();
            }
        }

        /// <summary>
        /// 异步保存转换设置到数据库
        /// </summary>
        private async Task SaveSettingsToDatabaseAsync(ConversionSettings settings)
        {
            try
            {
                var settingsJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await _databaseService.SetSettingAsync("ConversionSettings", settingsJson);
                Utils.Logger.Debug("ConversionSettingsService", "✅ 转换设置已保存到数据库");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsService", $"❌ 保存转换设置到数据库失败: {ex.Message}");
            }
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
        /// 加载默认设置 - 直接返回默认设置，不涉及文件操作
        /// </summary>
        private ConversionSettings LoadDefaultSettings()
        {
            try
            {
                Utils.Logger.Info("ConversionSettingsService", "🔧 创建默认转换设置");

                // 🔑 返回默认设置 - 使用ConversionOptions结构化选项
                var defaultSettings = CreateDefaultSettings();

                Utils.Logger.Info("ConversionSettingsService", "✅ 默认转换设置已创建");
                return defaultSettings;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsService", $"❌ 创建默认转换设置失败: {ex.Message}");
                return ConversionSettings.CreateDefault();
            }
        }

        /// <summary>
        /// 更新转换设置 - 保存到数据库确保一致性
        /// </summary>
        /// <param name="newSettings">新的转换设置</param>
        public void UpdateSettings(ConversionSettings newSettings)
        {
            try
            {
                Utils.Logger.Debug("ConversionSettingsService", "🔄 更新转换设置");

                // 🔧 保存到数据库（异步，不等待）
                SaveSettingsToDatabase(newSettings);

                // 🔧 立即更新当前设置
                _currentSettings = newSettings;
                OnPropertyChanged(nameof(CurrentSettings));

                // 🔧 触发设置变化事件
                SettingsChanged?.Invoke(this, new ConversionSettingsChangedEventArgs(newSettings));

                Utils.Logger.Info("ConversionSettingsService", "✅ 转换设置已更新并启动数据库保存");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsService", $"❌ 更新转换设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步获取设置 - 从数据库获取最新设置
        /// </summary>
        public async Task<ConversionSettings> GetSettingsAsync()
        {
            try
            {
                Utils.Logger.Debug("ConversionSettingsService", "🔍 异步从数据库获取转换设置");

                // 🔧 直接从数据库获取最新设置
                var settingsJson = await _databaseService.GetSettingAsync("ConversionSettings");

                if (!string.IsNullOrEmpty(settingsJson))
                {
                    var settings = JsonSerializer.Deserialize<ConversionSettings>(settingsJson);
                    if (settings != null)
                    {
                        _currentSettings = settings;
                        Utils.Logger.Debug("ConversionSettingsService", "✅ 异步从数据库获取转换设置成功");
                        return settings;
                    }
                }

                // 如果数据库中没有设置，返回当前设置或默认设置
                Utils.Logger.Debug("ConversionSettingsService", "📝 数据库中无设置，返回当前设置");
                return _currentSettings ?? CreateDefaultSettings();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsService", $"❌ 异步获取转换设置失败: {ex.Message}");
                return _currentSettings ?? CreateDefaultSettings();
            }
        }

        /// <summary>
        /// 异步保存设置 - 保存到数据库
        /// </summary>
        public async Task SaveSettingsAsync(ConversionSettings settings)
        {
            try
            {
                Utils.Logger.Debug("ConversionSettingsService", "💾 异步保存转换设置到数据库");

                // 🔧 保存到数据库
                await SaveSettingsToDatabaseAsync(settings);

                // 🔧 更新当前设置
                _currentSettings = settings;
                OnPropertyChanged(nameof(CurrentSettings));

                // 🔧 触发设置变化事件
                SettingsChanged?.Invoke(this, new ConversionSettingsChangedEventArgs(settings));

                Utils.Logger.Info("ConversionSettingsService", "✅ 转换设置已异步保存到数据库");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsService", $"❌ 异步保存转换设置失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 保存设置 - 使用数据库存储
        /// </summary>
        private void SaveSettings(ConversionSettings settings)
        {
            try
            {
                SaveSettingsToDatabase(settings);
                Utils.Logger.Debug("ConversionSettingsService", "✅ 转换设置已保存到数据库");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsService", $"❌ 保存转换设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存转换设置到数据库（同步版本）
        /// </summary>
        private void SaveSettingsToDatabase(ConversionSettings settings)
        {
            try
            {
                // 🔧 使用异步方法但不等待结果（用于同步调用场景）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SaveSettingsToDatabaseAsync(settings);
                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Error("ConversionSettingsService", $"❌ 异步保存转换设置失败: {ex.Message}");
                    }
                });

                Utils.Logger.Debug("ConversionSettingsService", "✅ 转换设置保存任务已启动");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsService", $"❌ 启动保存转换设置任务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建默认转换设置 - 使用ConversionOptions结构化选项
        /// </summary>
        private ConversionSettings CreateDefaultSettings()
        {
            return new ConversionSettings
            {
                // 基本设置 - 使用结构化选项的显示名称
                OutputFormat = ConversionOptions.GetDisplayNameByFormatValue("mp4"), // "MP4 (推荐)"
                Resolution = "保持原始",

                // 视频设置 - 使用结构化选项
                VideoCodec = "H.264 (CPU)",
                FrameRate = "保持原始",
                QualityMode = "恒定质量 (CRF)",
                VideoQuality = "23",
                EncodingPreset = "中等 (推荐)",
                Profile = "High",

                // 音频设置 - 使用结构化选项
                AudioCodec = "AAC (推荐)",
                AudioChannels = "保持原始",
                AudioQuality = "192 kbps (高质量)",
                SampleRate = "48 kHz (DVD质量)",
                AudioVolume = "0",

                // 高级设置
                HardwareAcceleration = "自动检测",
                PixelFormat = "YUV420P (标准)",
                ColorSpace = "BT.709 (HD)",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,

                // 滤镜设置
                Denoise = "无",
                VideoFilters = "",
                AudioFilters = "",

                // 任务设置
                Priority = 0,
                MaxRetries = 3
            };
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
