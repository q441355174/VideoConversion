using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// 转换设置服务 - 管理全局转换设置
    /// </summary>
    public class ConversionSettingsService : INotifyPropertyChanged
    {
        private static ConversionSettingsService? _instance;
        private static readonly object _lock = new object();

        private ConversionSettings _currentSettings;

        public static ConversionSettingsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ConversionSettingsService();
                        }
                    }
                }
                return _instance;
            }
        }

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

        /// <summary>
        /// 设置变化事件
        /// </summary>
        public event EventHandler<ConversionSettingsChangedEventArgs>? SettingsChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        private ConversionSettingsService()
        {
            // 初始化默认设置
            _currentSettings = LoadDefaultSettings();

            // 记录服务初始化
            System.Diagnostics.Debug.WriteLine($"ConversionSettingsService 已初始化，设置: {_currentSettings.VideoCodec}, {_currentSettings.Resolution}");
        }

        /// <summary>
        /// 初始化服务（在程序启动时调用）
        /// </summary>
        public static void Initialize()
        {
            // 触发单例创建，确保服务在程序启动时就存在
            var _ = Instance;
            System.Diagnostics.Debug.WriteLine("ConversionSettingsService 已预初始化");
        }

        /// <summary>
        /// 加载默认转换设置
        /// </summary>
        private ConversionSettings LoadDefaultSettings()
        {
            try
            {
                // 尝试从数据库加载设置
                var savedSettings = LoadSettingsFromDatabase();
                if (savedSettings != null)
                {
                    System.Diagnostics.Debug.WriteLine("从数据库加载转换设置成功");
                    return savedSettings;
                }

                // 如果数据库中没有设置，返回默认设置
                var defaultSettings = new ConversionSettings
                {
                    VideoCodec = "H.264",
                    Resolution = "1920x1080",
                    FrameRate = "30",
                    Bitrate = "5000k",
                    AudioCodec = "AAC",
                    AudioQuality = "128k",
                    HardwareAcceleration = "自动",
                    Threads = "自动"
                };

                // 保存默认设置到数据库
                SaveSettingsToDatabase(defaultSettings);
                System.Diagnostics.Debug.WriteLine("使用默认转换设置并保存到数据库");

                return defaultSettings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载默认转换设置失败: {ex.Message}");
                return new ConversionSettings();
            }
        }

        /// <summary>
        /// 更新转换设置
        /// </summary>
        /// <param name="newSettings">新的转换设置</param>
        public void UpdateSettings(ConversionSettings newSettings)
        {
            try
            {
                CurrentSettings = newSettings;
                SaveSettings(newSettings);
                System.Diagnostics.Debug.WriteLine("转换设置已更新");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新转换设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存设置到持久化存储
        /// </summary>
        private void SaveSettings(ConversionSettings settings)
        {
            try
            {
                SaveSettingsToDatabase(settings);
                System.Diagnostics.Debug.WriteLine($"保存转换设置: {settings.VideoCodec}, {settings.Resolution}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存转换设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从配置文件加载设置
        /// </summary>
        private ConversionSettings? LoadSettingsFromDatabase()
        {
            try
            {
                var configPath = GetConfigFilePath();
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var settings = JsonSerializer.Deserialize<ConversionSettings>(json);
                    return settings;
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从配置文件加载转换设置失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 保存设置到配置文件
        /// </summary>
        private void SaveSettingsToDatabase(ConversionSettings settings)
        {
            try
            {
                var configPath = GetConfigFilePath();
                var configDir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(configPath, json);

                System.Diagnostics.Debug.WriteLine($"保存转换设置到配置文件: {configPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存转换设置到配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        private string GetConfigFilePath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "VideoConversion-Client");
            return Path.Combine(appFolder, "conversion-settings.json");
        }

        /// <summary>
        /// 获取目标分辨率的宽高
        /// </summary>
        public (int width, int height) GetTargetResolution()
        {
            try
            {
                var resolution = CurrentSettings.Resolution;
                if (resolution.Contains("x"))
                {
                    var parts = resolution.Split('x');
                    if (parts.Length == 2 && 
                        int.TryParse(parts[0], out var width) && 
                        int.TryParse(parts[1], out var height))
                    {
                        return (width, height);
                    }
                }
                
                // 默认返回1920x1080
                return (1920, 1080);
            }
            catch
            {
                return (1920, 1080);
            }
        }

        /// <summary>
        /// 获取目标比特率（bps）
        /// </summary>
        public long GetTargetBitrate()
        {
            try
            {
                var bitrate = CurrentSettings.Bitrate;
                if (bitrate.EndsWith("k", StringComparison.OrdinalIgnoreCase))
                {
                    var value = bitrate.Substring(0, bitrate.Length - 1);
                    if (double.TryParse(value, out var kbps))
                    {
                        return (long)(kbps * 1000);
                    }
                }
                else if (bitrate.EndsWith("M", StringComparison.OrdinalIgnoreCase))
                {
                    var value = bitrate.Substring(0, bitrate.Length - 1);
                    if (double.TryParse(value, out var mbps))
                    {
                        return (long)(mbps * 1000000);
                    }
                }
                else if (long.TryParse(bitrate, out var bps))
                {
                    return bps;
                }

                // 默认5Mbps
                return 5000000;
            }
            catch
            {
                return 5000000;
            }
        }

        /// <summary>
        /// 预估转换后的文件大小
        /// </summary>
        /// <param name="originalDuration">原始时长（秒）</param>
        /// <param name="originalSize">原始文件大小（字节）</param>
        /// <returns>预估文件大小（字节）</returns>
        public long EstimateConvertedFileSize(double originalDuration, long originalSize)
        {
            try
            {
                if (originalDuration <= 0) return originalSize;

                var targetBitrate = GetTargetBitrate();
                var audioBitrate = GetAudioBitrate();
                
                // 计算总比特率
                var totalBitrate = targetBitrate + audioBitrate;
                
                // 预估文件大小 = 比特率 * 时长 / 8
                var estimatedSize = (long)(totalBitrate * originalDuration / 8);
                
                return estimatedSize;
            }
            catch
            {
                return originalSize;
            }
        }

        /// <summary>
        /// 获取音频比特率（bps）
        /// </summary>
        private long GetAudioBitrate()
        {
            try
            {
                var audioQuality = CurrentSettings.AudioQuality;
                if (audioQuality.EndsWith("k", StringComparison.OrdinalIgnoreCase))
                {
                    var value = audioQuality.Substring(0, audioQuality.Length - 1);
                    if (double.TryParse(value, out var kbps))
                    {
                        return (long)(kbps * 1000);
                    }
                }
                
                // 默认128kbps
                return 128000;
            }
            catch
            {
                return 128000;
            }
        }

        /// <summary>
        /// 格式化分辨率显示
        /// </summary>
        public string GetFormattedResolution()
        {
            var (width, height) = GetTargetResolution();
            return $"{width}×{height}";
        }

        /// <summary>
        /// 程序关闭时的清理工作
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // 确保最新设置已保存
                SaveSettings(_currentSettings);
                System.Diagnostics.Debug.WriteLine("ConversionSettingsService 清理完成，设置已保存");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConversionSettingsService 清理失败: {ex.Message}");
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 转换设置变化事件参数
    /// </summary>
    public class ConversionSettingsChangedEventArgs : EventArgs
    {
        public ConversionSettings NewSettings { get; }

        public ConversionSettingsChangedEventArgs(ConversionSettings newSettings)
        {
            NewSettings = newSettings;
        }
    }
}
