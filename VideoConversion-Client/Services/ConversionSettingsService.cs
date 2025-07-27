using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
                    AudioQualityMode = "bitrate",
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
                // 使用VideoQuality作为比特率参考，如果是CRF模式则返回估算值
                var qualityMode = CurrentSettings.QualityMode;
                if (qualityMode?.Contains("CRF") == true)
                {
                    // CRF模式，返回估算的比特率（基于CRF值）
                    if (int.TryParse(CurrentSettings.VideoQuality, out int crfValue))
                    {
                        // CRF值越低，质量越高，比特率越高
                        // 这是一个粗略的估算公式：CRF 18 ≈ 8Mbps, CRF 23 ≈ 5Mbps, CRF 28 ≈ 2Mbps
                        return (long)((51 - crfValue) * 200 * 1000); // 200k per CRF point
                    }
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
        /// 获取支持的输出格式列表
        /// </summary>
        public static List<FormatOption> GetSupportedFormats()
        {
            return new List<FormatOption>
            {
                // 推荐格式
                new FormatOption("mp4", "MP4 (推荐)", "最佳兼容性，通用格式", true),
                new FormatOption("mkv", "MKV (高质量)", "开源容器，支持多轨道", true),
                new FormatOption("webm", "WebM (Web优化)", "Web播放优化，文件较小", true),

                // 通用格式
                new FormatOption("avi", "AVI (兼容性)", "传统格式，广泛支持", false),
                new FormatOption("mov", "MOV (Apple)", "Apple QuickTime格式", false),
                new FormatOption("m4v", "M4V (iTunes)", "iTunes视频格式", false),

                // 移动设备格式
                new FormatOption("3gp", "3GP (移动设备)", "移动设备兼容格式", false),

                // 传统格式
                new FormatOption("wmv", "WMV (Windows)", "Windows Media格式", false),
                new FormatOption("flv", "FLV (Flash)", "Flash视频格式", false),

                // 广播格式
                new FormatOption("mpg", "MPEG (标准)", "标准MPEG格式", false),
                new FormatOption("ts", "TS (传输流)", "传输流格式", false),
                new FormatOption("mts", "MTS (AVCHD)", "AVCHD摄像机格式", false),
                new FormatOption("m2ts", "M2TS (蓝光)", "蓝光光盘格式", false),

                // 特殊格式
                new FormatOption("vob", "VOB (DVD)", "DVD视频格式", false),
                new FormatOption("asf", "ASF (Windows Media)", "Windows Media格式", false),

                // 智能选项
                new FormatOption("keep_original", "保持原格式", "与源文件相同格式", false),
                new FormatOption("auto_best", "自动选择最佳格式", "根据内容自动选择", false)
            };
        }

        /// <summary>
        /// 验证输出格式是否支持
        /// </summary>
        public static bool IsFormatSupported(string format)
        {
            var supportedFormats = GetSupportedFormats();
            return supportedFormats.Any(f => f.Value.Equals(format, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取格式的显示名称
        /// </summary>
        public static string GetFormatDisplayName(string format)
        {
            var formatOption = GetSupportedFormats()
                .FirstOrDefault(f => f.Value.Equals(format, StringComparison.OrdinalIgnoreCase));
            return formatOption?.DisplayName ?? format.ToUpperInvariant();
        }

        /// <summary>
        /// 获取格式建议
        /// </summary>
        public static string GetFormatRecommendation(string format)
        {
            var formatOption = GetSupportedFormats()
                .FirstOrDefault(f => f.Value.Equals(format, StringComparison.OrdinalIgnoreCase));
            return formatOption?.Description ?? "标准视频格式";
        }

        /// <summary>
        /// 处理智能格式选择
        /// </summary>
        public static string ResolveSmartFormat(string selectedFormat, string originalFilePath)
        {
            return selectedFormat switch
            {
                "keep_original" => GetOriginalFormat(originalFilePath),
                "auto_best" => GetBestFormatForFile(originalFilePath),
                _ => selectedFormat
            };
        }

        /// <summary>
        /// 获取原始文件格式
        /// </summary>
        private static string GetOriginalFormat(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return "mp4";

            var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
            return IsFormatSupported(extension) ? extension : "mp4";
        }

        /// <summary>
        /// 为文件选择最佳格式
        /// </summary>
        private static string GetBestFormatForFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return "mp4";

            var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

            // 根据原始格式推荐最佳输出格式
            return extension switch
            {
                "avi" or "wmv" or "flv" => "mp4",  // 传统格式转为MP4
                "mov" or "m4v" => "mp4",           // Apple格式转为MP4
                "ts" or "mts" or "m2ts" => "mkv",  // 广播格式转为MKV
                "vob" => "mp4",                    // DVD格式转为MP4
                "rm" or "rmvb" => "mp4",           // 专有格式转为MP4
                "webm" => "webm",                  // WebM保持WebM
                "mkv" => "mkv",                    // MKV保持MKV
                _ => "mp4"                         // 默认MP4
            };
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

    /// <summary>
    /// 格式选项数据模型
    /// </summary>
    public class FormatOption
    {
        public string Value { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public bool IsRecommended { get; set; }

        public FormatOption(string value, string displayName, string description, bool isRecommended = false)
        {
            Value = value;
            DisplayName = displayName;
            Description = description;
            IsRecommended = isRecommended;
        }
    }
}
